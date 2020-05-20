using System;
using System.Collections.Generic;
using System.Linq;
using Twino.Client.TMQ.Models;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Exception thrown handler for message reader
    /// </summary>
    public delegate void ReaderExceptionThrownHandler(TmqMessage message, Exception exception);

    /// <summary>
    /// Type based message reader for TMQ client
    /// </summary>
    public class MessageReader
    {
        #region Fields

        /// <summary>
        /// All message subscriptions
        /// </summary>
        private readonly List<ReadSubscription> _subscriptions = new List<ReadSubscription>(16);

        /// <summary>
        /// Attached TMQ clients
        /// </summary>
        private readonly List<TmqClient> _attachedClients = new List<TmqClient>(8);

        /// <summary>
        /// TmqMessage to model type converter function
        /// </summary>
        private Func<TmqMessage, Type, object> _func;

        /// <summary>
        /// This event is triggered when user defined action throws an exception
        /// </summary>
        public event ReaderExceptionThrownHandler OnException;

        #endregion

        #region Create

        /// <summary>
        /// Creates new message reader with converter action
        /// </summary>
        public MessageReader(Func<TmqMessage, Type, object> func)
        {
            _func = func;
        }

        /// <summary>
        /// Creates new message reader, reads UTF-8 string from message content and deserializes it with System.Text.Json
        /// </summary>
        public static MessageReader JsonReader()
        {
            return new MessageReader((msg, type) => System.Text.Json.JsonSerializer.Deserialize(msg.ToString(), type));
        }

        #endregion

        #region Attach - Detach

        /// <summary>
        /// Attach the client to the message reader and starts to read messages
        /// </summary>
        public void Attach(TmqClient client)
        {
            client.MessageReceived += ClientOnMessageReceived;

            lock (_attachedClients)
                _attachedClients.Add(client);
        }

        /// <summary>
        /// Detach the client from reading it's messages
        /// </summary>
        public void Detach(TmqClient client)
        {
            client.MessageReceived -= ClientOnMessageReceived;

            lock (_attachedClients)
                _attachedClients.Remove(client);
        }

        /// <summary>
        /// Detach all clients
        /// </summary>
        public void DetachAll()
        {
            lock (_attachedClients)
            {
                foreach (TmqClient client in _attachedClients)
                    client.MessageReceived += ClientOnMessageReceived;

                _attachedClients.Clear();
            }
        }

        #endregion

        #region Read

        /// <summary>
        /// Reads the received model, if there is subscription to the model, trigger the actions.
        /// Use this method when you can't attach the client easily or directly. (ex: use for connections)
        /// </summary>
        public void Read(TmqClient sender, TmqMessage message)
        {
            ClientOnMessageReceived(sender, message);
        }

        /// <summary>
        /// When a message received to Tmq Client, this method will be called
        /// </summary>
        private void ClientOnMessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage message)
        {
            ReadSource source;

            if (message.Type == MessageType.QueueMessage)
            {
                if (string.IsNullOrEmpty(message.Target))
                    return;

                source = ReadSource.Queue;
            }
            else if (message.Type == MessageType.DirectMessage)
                source = ReadSource.Direct;
            else
                return;

            //find all subscriber actions
            List<ReadSubscription> subs;
            lock (_subscriptions)
            {
                subs = _subscriptions.Where(x => x.Source == source &&
                                                 x.ContentType == message.ContentType &&
                                                 x.Channel.Equals(message.Target, StringComparison.InvariantCultureIgnoreCase))
                                     .ToList();
            }

            if (subs.Count == 0)
                return;

            //convert model, only one time to first susbcriber's type
            Type type = subs[0].MessageType;
            object model = _func(message, type);

            //call all subscriber methods if they have same type
            foreach (ReadSubscription sub in subs)
            {
                if (sub.MessageType == type)
                {
                    try
                    {
                        if (sub.TmqMessageParameter)
                            sub.Action.DynamicInvoke(model, message);
                        else
                            sub.Action.DynamicInvoke(model);
                    }
                    catch (Exception ex)
                    {
                        OnException?.Invoke(message, ex);
                    }
                }
            }
        }

        #endregion

        #region Subscriptions

        /// <summary>
        /// Subscribe to messages in a queue in a channel
        /// </summary>
        public void On<T>(string channel, ushort queueId, Action<T> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Queue,
                                                Channel = channel,
                                                ContentType = queueId,
                                                MessageType = typeof(T),
                                                Action = action
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to messages in a queue in a channel
        /// </summary>
        public void On<T>(string channel, ushort queueId, Action<T, TmqMessage> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Queue,
                                                Channel = channel,
                                                ContentType = queueId,
                                                MessageType = typeof(T),
                                                Action = action,
                                                TmqMessageParameter = true
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to direct messages with a content type
        /// </summary>
        public void OnDirect<T>(ushort contentType, Action<T> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Direct,
                                                ContentType = contentType,
                                                MessageType = typeof(T),
                                                Action = action
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Subscribe to direct messages with a content type
        /// </summary>
        public void OnDirect<T>(ushort contentType, Action<T, TmqMessage> action)
        {
            ReadSubscription subscription = new ReadSubscription
                                            {
                                                Source = ReadSource.Direct,
                                                ContentType = contentType,
                                                MessageType = typeof(T),
                                                Action = action,
                                                TmqMessageParameter = true
                                            };

            lock (_subscriptions)
                _subscriptions.Add(subscription);
        }

        /// <summary>
        /// Unsubscribe from messages in a queue in a channel 
        /// </summary>
        public void Off(string channel, ushort queueId)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.Source == ReadSource.Queue &&
                                              x.ContentType == queueId
                                              && x.Channel.Equals(channel, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Unsubscribe from direct messages with a content type 
        /// </summary>
        public void Off(ushort contentType)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.Source == ReadSource.Direct && x.ContentType == contentType);
        }

        /// <summary>
        /// Clear all subscriptions for the channels and direct messages
        /// </summary>
        public void Clear(string channel)
        {
            lock (_subscriptions)
                _subscriptions.RemoveAll(x => x.Channel.Equals(channel, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Clear all subscription for this instance
        /// </summary>
        public void ClearAll()
        {
            lock (_subscriptions)
                _subscriptions.Clear();
        }

        #endregion
    }
}