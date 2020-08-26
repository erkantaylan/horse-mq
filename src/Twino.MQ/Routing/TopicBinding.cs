using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Topic binding targets channels with topics.
    /// Messages are pushed to queues in channels with topic.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class TopicBinding : Binding
    {
        private TwinoQueue[] _queues;
        private DateTime _queueUpdateTime;
        private readonly TimeSpan _queueCacheDuration = TimeSpan.FromMilliseconds(250);
        private readonly IUniqueIdGenerator _idGenerator = new DefaultUniqueIdGenerator();
        private int _roundRobinIndex = -1;

        /// <summary>
        /// Tag binding routing method
        /// </summary>
        public RouteMethod RouteMethod { get; set; }

        /// <summary>
        /// Creates new direct binding.
        /// Name is the name of the binding.
        /// Target is the topic of channels.
        /// Content Type should be Queue Id.
        /// Priority for router binding.
        /// </summary>
        public TopicBinding(string name, string target, ushort? contentType, int priority, BindingInteraction interaction,
                            RouteMethod routeMethod = RouteMethod.Distribute)
            : base(name, target, contentType, priority, interaction)
        {
            RouteMethod = routeMethod;
        }

        /// <summary>
        /// Sends the message to binding receivers
        /// </summary>
        public override Task<bool> Send(MqClient sender, TwinoMessage message)
        {
            if (DateTime.UtcNow - _queueUpdateTime > _queueCacheDuration)
                RefreshQueueCache();

            message.WaitResponse = Interaction == BindingInteraction.Response;

            switch (RouteMethod)
            {
                case RouteMethod.Distribute:
                    return SendDistribute(message);

                case RouteMethod.OnlyFirst:
                    return SendOnlyFirst(message);

                case RouteMethod.RoundRobin:
                    return SendRoundRobin(message);

                default:
                    return Task.FromResult(false);
            }
        }

        private async Task<bool> SendDistribute(TwinoMessage message)
        {
            ushort queueId = ContentType.HasValue ? ContentType.Value : message.ContentType;
            bool sent = false;
            foreach (TwinoQueue queue in _queues)
            {
                string messageId = sent || Interaction == BindingInteraction.None
                                       ? message.MessageId
                                       : _idGenerator.Create();

                if (!sent)
                    sent = true;

                TwinoMessage msg = message.Clone(true, true, messageId);
                QueueMessage queueMessage = new QueueMessage(msg);
                queue.AddMessage(queueMessage);
            }

            return sent;
        }

        private async Task<bool> SendRoundRobin(TwinoMessage message)
        {
            Interlocked.Increment(ref _roundRobinIndex);
            int i = _roundRobinIndex;

            if (i >= _queues.Length)
            {
                _roundRobinIndex = 0;
                i = 0;
            }

            if (_queues.Length == 0)
                return false;

            TwinoQueue queue = Router.Server.FindQueue(message.Target);
            if (queue == null)
            {
                if (!Router.Server.Options.AutoQueueCreation)
                    return false;

                queue = await Router.Server.CreateQueue(message.Target, Router.Server.Options, message, Router.Server.DeliveryHandlerFactory);
            }

            QueueMessage queueMessage = new QueueMessage(message);
            queue.AddMessage(queueMessage);
            return true;
        }

        private async Task<bool> SendOnlyFirst(TwinoMessage message)
        {
            if (_queues.Length < 1)
                return false;

            TwinoQueue queue = Router.Server.FindQueue(message.Target);
            if (queue == null)
            {
                if (!Router.Server.Options.AutoQueueCreation)
                    return false;

                queue = await Router.Server.CreateQueue(message.Target, Router.Server.Options, message, Router.Server.DeliveryHandlerFactory);
            }

            QueueMessage queueMessage = new QueueMessage(message);
            queue.AddMessage(queueMessage);
            return true;
        }

        private void RefreshQueueCache()
        {
            _queueUpdateTime = DateTime.UtcNow;
            _queues = Router.Server.Queues.Where(x => x.Topic != null && Filter.CheckMatch(x.Topic, Target)).ToArray();
        }
    }
}