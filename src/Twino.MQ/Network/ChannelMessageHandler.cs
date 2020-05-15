using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class ChannelMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public ChannelMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            //find channel and queue
            Channel channel = _server.FindChannel(message.Target);

            //if auto creation active, try to create channel
            if (channel == null && _server.Options.AutoChannelCreation)
                channel = _server.FindOrCreateChannel(message.Target);

            if (channel == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TmqResponseCode.NotFound));
                return;
            }

            ChannelQueue queue = channel.FindQueue(message.ContentType);

            //if auto creation active, try to create queue
            if (queue == null && _server.Options.AutoQueueCreation)
                queue = await channel.FindOrCreateQueue(message.ContentType);

            if (queue == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TmqResponseCode.NotFound));
                return;
            }

            //consumer is trying to pull from the queue
            //in false cases, we won't send any response, cuz client is pending only queue messages, not response messages
            if (message.Length == 0 && message.PendingResponse)
                await HandlePullRequest(client, message, channel, queue);

            //message have a content, this is the real message from producer to the queue
            else
                await HandlePush(client, message, queue);
        }

        /// <summary>
        /// Handles pulling a message from a queue
        /// </summary>
        private async Task HandlePullRequest(MqClient client, TmqMessage message, Channel channel, ChannelQueue queue)
        {
            //only pull statused queues can handle this request
            if (queue.Status != QueueStatus.Pull)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TmqResponseCode.BadRequest));
                return;
            }

            //client cannot pull message from the channel not in
            ChannelClient channelClient = channel.FindClient(client);
            if (channelClient == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TmqResponseCode.Unauthorized));
                return;
            }

            //check authorization
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanPullFromQueue(channelClient, queue);
                if (!grant)
                {
                    if (!string.IsNullOrEmpty(message.MessageId))
                        await client.SendAsync(message.CreateResponse(TmqResponseCode.Unauthorized));
                    return;
                }
            }

            await queue.State.Pull(channelClient, message);
        }

        /// <summary>
        /// Handles pushing a message into a queue
        /// </summary>
        private async Task HandlePush(MqClient client, TmqMessage message, ChannelQueue queue)
        {
            //check authority
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanMessageToQueue(client, queue, message);
                if (!grant)
                {
                    if (!string.IsNullOrEmpty(message.MessageId))
                        await client.SendAsync(message.CreateResponse(TmqResponseCode.Unauthorized));
                    return;
                }
            }

            //prepare the message
            QueueMessage queueMessage = new QueueMessage(message);
            queueMessage.Source = client;

            //push the message
            PushResult result = await queue.Push(queueMessage, client);
            if (result == PushResult.StatusNotSupported)
                await client.SendAsync(message.CreateResponse(TmqResponseCode.Unauthorized));
            else if (result == PushResult.LimitExceeded)
                await client.SendAsync(message.CreateResponse(TmqResponseCode.LimitExceeded));
        }
    }
}