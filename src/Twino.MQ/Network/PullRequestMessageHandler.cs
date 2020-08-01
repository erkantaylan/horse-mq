using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class PullRequestMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly TwinoMQ _server;

        public PullRequestMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message, bool fromNode)
        {
            //find channel and queue
            Channel channel = _server.FindChannel(message.Target);

            //if auto creation active, try to create channel
            if (channel == null && _server.Options.AutoChannelCreation)
                channel = _server.FindOrCreateChannel(message.Target);

            if (channel == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            ChannelQueue queue = channel.FindQueue(message.ContentType);

            //if auto creation active, try to create queue
            if (queue == null && _server.Options.AutoQueueCreation)
            {
                ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(channel.Options);
                queue = await channel.CreateQueue(message.ContentType, options, message, channel.Server.DeliveryHandlerFactory);
            }

            if (queue == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return;
            }

            await HandlePullRequest(client, message, channel, queue);
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
                    await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, TmqHeaders.UNACCEPTABLE));
                
                return;
            }

            //client cannot pull message from the channel not in
            ChannelClient channelClient = channel.FindClient(client);
            if (channelClient == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, TmqHeaders.UNAUTHORIZED));
                return;
            }

            //check authorization
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanPullFromQueue(channelClient, queue);
                if (!grant)
                {
                    if (!string.IsNullOrEmpty(message.MessageId))
                        await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, TmqHeaders.UNAUTHORIZED));
                    return;
                }
            }

            await queue.State.Pull(channelClient, message);
        }
    }
}