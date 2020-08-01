using System;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Single message connector for TMQ protocol.
    /// </summary>
    public class TmqSingleMessageConnector : SingleMessageConnector<TmqClient, TmqMessage>
    {
        private readonly MessageObserver _observer;

        /// <summary>
        /// Default TMQ Message reader for connector
        /// </summary>
        public MessageObserver Observer => _observer;

        /// <summary>
        /// If true, automatically joins all subscribed channels
        /// </summary>
        public bool AutoJoinConsumerChannels { get; set; }

        /// <summary>
        /// Content Serializer for clients in this connector.
        /// If null, default content serializer will be used.
        /// </summary>
        public IMessageContentSerializer ContentSerializer { get; set; }

        /// <summary>
        /// Creates new single message connector for TMQ protocol clients
        /// </summary>
        public TmqSingleMessageConnector(Func<TmqClient> createInstance = null) : base(createInstance)
        {
            _observer = new MessageObserver(ReadMessage);
        }

        private object ReadMessage(TmqMessage message, Type type)
        {
            if (ContentSerializer == null)
                ContentSerializer = new NewtonsoftContentSerializer();
            
            return ContentSerializer.Deserialize(message, type);
        }

        /// <inheritdoc />
        protected override void ClientConnected(SocketBase client)
        {
            if (ContentSerializer != null)
            {
                if (client is TmqClient tmqClient)
                    tmqClient.JsonSerializer = ContentSerializer;
            }

            base.ClientConnected(client);
        }

        /// <inheritdoc />
        protected override void ClientMessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage payload)
        {
            base.ClientMessageReceived(client, payload);

            if (_observer != null)
                _observer.Read((TmqClient) client, payload);
        }

        #region On - Consume

        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(Action<T> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(channel, content, action);
        }

        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(Action<T, TmqMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T, TmqMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(channel, content, action);
        }

        #endregion

        #region OnDirect - Consume

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(Action<T> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(content, action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(Action<T, TmqMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T, TmqMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(content, action);
        }

        #endregion

        #region Off

        /// <summary>
        /// Unsubscribes from reading messages in a queue
        /// </summary>
        public void Off<T>()
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.Off<T>();
        }

        /// <summary>
        /// Unsubscribes from reading messages in a queue
        /// </summary>
        public void Off(string channel, ushort content)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.Off(channel, content);
        }

        /// <summary>
        /// Unsubscribes from reading direct messages
        /// </summary>
        public void OffDirect<T>()
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OffDirect<T>();
        }

        /// <summary>
        /// Unsubscribes from reading direct messages
        /// </summary>
        public void OffDirect(ushort content)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OffDirect(content);
        }

        #endregion

        #region Send

        /// <summary>
        /// Sends a message
        /// </summary>
        public bool Send(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Send(message);

            return false;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult> SendAsync(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendAsync(message);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult> SendDirectJsonAsync<T>(T model, bool waitForAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendJsonAsync(MessageType.DirectMessage, model, waitForAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult> SendDirectJsonAsync<T>(string target, ushort contentType, T model, bool waitForAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendJsonAsync(MessageType.DirectMessage, target, contentType, model, waitForAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        #endregion

        #region Request

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TmqMessage> RequestAsync(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Request(message);

            return null;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(TRequest request)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.RequestJson<TResponse>(request);

            return null;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target, ushort contentType, TRequest request)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.RequestJson<TResponse>(target, contentType, request);

            return null;
        }

        #endregion

        #region Push

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> Push(string channel, ushort contentType, MemoryStream content, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.Push(channel, contentType, content, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> PushJson(object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> PushJson(string channel, ushort contentType, object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(channel, contentType, jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        #endregion

        #region Publish

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<TwinoResult> Publish(string routerName, MemoryStream content, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.Publish(routerName, content.ToArray(), waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<TwinoResult> PublishJson(object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.PublishJson(jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<TwinoResult> PublishJson(string routerName, object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.PublishJson(routerName, jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        #endregion
    }
}