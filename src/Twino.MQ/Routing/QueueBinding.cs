using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Queue message binding.
    /// Targets channel queues.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class QueueBinding : Binding
    {
        private TwinoQueue _targetQueue;
        private DateTime _queueUpdateTime;

        /// <summary>
        /// Creates new direct binding.
        /// Name is the name of the binding.
        /// Target should be channel name.
        /// Content Type should be Queue Id.
        /// Priority for router binding.
        /// </summary>
        public QueueBinding(string name, string target, ushort contentType, int priority, BindingInteraction interaction)
            : base(name, target, contentType, priority, interaction)
        {
        }

        /// <summary>
        /// Sends the message to binding receivers
        /// </summary>
        public override async Task<bool> Send(MqClient sender, TwinoMessage message)
        {
            TwinoQueue queue = GetQueue();
            if (queue == null)
                return false;

            string messageId = Interaction == BindingInteraction.None
                                   ? Router.Server.MessageIdGenerator.Create()
                                   : message.MessageId;

            TwinoMessage msg = message.Clone(true, true, messageId);

            msg.Type = MessageType.QueueMessage;
            msg.SetTarget(Target);

            // ReSharper disable once PossibleInvalidOperationException
            msg.ContentType = ContentType.Value;
            message.PendingResponse = Interaction == BindingInteraction.Response;

            QueueMessage queueMessage = new QueueMessage(msg);
            queueMessage.Source = sender;

            PushResult result = await _targetQueue.Push(queueMessage, sender);
            return result == PushResult.Success;
        }

        /// <summary>
        /// Gets queue.
        /// If it's not cached, finds and caches it before returns.
        /// </summary>
        /// <returns></returns>
        private TwinoQueue GetQueue()
        {
            if (_targetQueue != null && DateTime.UtcNow - _queueUpdateTime < TimeSpan.FromMinutes(1))
                return _targetQueue;

            TwinoQueue queue = Router.Server.FindQueue(Target);
            if (queue == null)
                return null;

            _queueUpdateTime = DateTime.UtcNow;
            _targetQueue = queue;
            return _targetQueue;
        }
    }
}