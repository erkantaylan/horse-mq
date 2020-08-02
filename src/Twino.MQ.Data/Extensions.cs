using System;
using System.IO;
using System.Threading.Tasks;
using Twino.MQ.Data.Configuration;
using Twino.MQ.Options;
using Twino.MQ.Queues;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Object for persistent queue extension methods
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Adds persistent queues with default configuration
        /// </summary>
        public static TwinoMQ AddPersistentQueues(this TwinoMQ server)
        {
            return AddPersistentQueues(server, c => { });
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static TwinoMqBuilder AddPersistentQueues(this TwinoMqBuilder builder)
        {
            return AddPersistentQueues(builder, c => { });
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static TwinoMqBuilder AddPersistentQueues(this TwinoMqBuilder builder,
                                                         Action<DataConfigurationBuilder> cfg)
        {
            builder.Server.AddPersistentQueues(cfg);
            return builder;
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static TwinoMQ AddPersistentQueues(this TwinoMQ server,
                                                  Action<DataConfigurationBuilder> cfg)
        {
            DataConfigurationBuilder builder = new DataConfigurationBuilder();
            cfg(builder);

            if (builder.GenerateQueueFilename == null)
                builder.GenerateQueueFilename = DefaultQueueDbPath;

            ConfigurationFactory.Initialize(builder);

            return server;
        }

        /// <summary>
        /// Loads all persistent queue messages from databases
        /// </summary>
        public static Task LoadPersistentQueues(this TwinoMQ server)
        {
            if (ConfigurationFactory.Builder == null)
                throw new InvalidOperationException("Before loading queues initialize persistent queues with AddPersistentQueues method");

            return ConfigurationFactory.Manager.LoadQueues(server);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="builder">Twino MQ Builder</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="producerAckDecision">Decision when producer receives acknowledge</param>
        /// <returns></returns>
        public static TwinoMqBuilder UsePersistentDeliveryHandler(this TwinoMqBuilder builder,
                                                                  DeleteWhen deleteWhen,
                                                                  ProducerAckDecision producerAckDecision)
        {
            builder.Server.DeliveryHandlerFactory = async (dh) =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(dh.Queue);
                PersistentDeliveryHandler handler = new PersistentDeliveryHandler(dh.Queue, databaseOptions, deleteWhen, producerAckDecision);
                await handler.Initialize();
                dh.OnAfterCompleted(AfterDeliveryHandlerCreated);
                return handler;
            };
            return builder;
        }

        /// <summary>
        /// Creates and initializes new persistent delivery handler for the queue
        /// </summary>
        /// <param name="builder">Delivery handler builder</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="producerAckDecision">Decision when producer receives acknowledge</param>
        /// <returns></returns>
        public static async Task<IMessageDeliveryHandler> CreatePersistentDeliveryHandler(this DeliveryHandlerBuilder builder,
                                                                                          DeleteWhen deleteWhen,
                                                                                          ProducerAckDecision producerAckDecision)
        {
            DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
            PersistentDeliveryHandler handler = new PersistentDeliveryHandler(builder.Queue, databaseOptions, deleteWhen, producerAckDecision);
            await handler.Initialize();
            builder.OnAfterCompleted(AfterDeliveryHandlerCreated);
            return handler;
        }

        private static void AfterDeliveryHandlerCreated(DeliveryHandlerBuilder builder)
        {
            PersistentDeliveryHandler persistentHandler = builder.Queue.DeliveryHandler as PersistentDeliveryHandler;
            if (persistentHandler == null)
                return;

            bool added = ConfigurationFactory.Manager.Add(builder.Queue, persistentHandler.Database.File.Filename);
            if (added)
                ConfigurationFactory.Manager.Save();
        }

        /// <summary>
        /// Creates new persistent queue in the channel
        /// </summary>
        /// <param name="mq">Twino MQ</param>
        /// <param name="channelName">Name of the channel. Created, if it isn't exists</param>
        /// <param name="queueId">Queue Id</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="producerAckDecision">Decision, when ack will be sent to producer</param>
        /// <returns></returns>
        public static Task<ChannelQueue> CreatePersistentQueue(this TwinoMQ mq,
                                                               string channelName,
                                                               ushort queueId,
                                                               DeleteWhen deleteWhen,
                                                               ProducerAckDecision producerAckDecision)
        {
            Channel channel = mq.FindOrCreateChannel(channelName);
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(channel.Options);
            return CreatePersistentQueue(channel, queueId, deleteWhen, producerAckDecision, options);
        }

        /// <summary>
        /// Creates new persistent queue in the channel
        /// </summary>
        /// <param name="channel">The channel queue will be created in</param>
        /// <param name="queueId">Queue Id</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="producerAckDecision">Decision, when ack will be sent to producer</param>
        /// <returns></returns>
        public static Task<ChannelQueue> CreatePersistentQueue(this Channel channel,
                                                               ushort queueId,
                                                               DeleteWhen deleteWhen,
                                                               ProducerAckDecision producerAckDecision)
        {
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(channel.Options);
            return CreatePersistentQueue(channel, queueId, deleteWhen, producerAckDecision, options);
        }

        /// <summary>
        /// Creates new persistent queue in the channel
        /// </summary>
        /// <param name="channel">The channel queue will be created in</param>
        /// <param name="queueId">Queue Id</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="producerAckDecision">Decision, when ack will be sent to producer</param>
        /// <param name="optionsAction">Channel Queue Options builder action</param>
        /// <returns></returns>
        public static Task<ChannelQueue> CreatePersistentQueue(this Channel channel,
                                                               ushort queueId,
                                                               DeleteWhen deleteWhen,
                                                               ProducerAckDecision producerAckDecision,
                                                               Action<ChannelQueueOptions> optionsAction)
        {
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(channel.Options);
            optionsAction(options);
            return CreatePersistentQueue(channel, queueId, deleteWhen, producerAckDecision, options);
        }

        /// <summary>
        /// Creates new persistent queue in the channel
        /// </summary>
        /// <param name="channel">The channel queue will be created in</param>
        /// <param name="queueId">Queue Id</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="producerAckDecision">Decision, when ack will be sent to producer</param>
        /// <param name="options">Channel Queue Options</param>
        /// <returns></returns>
        public static async Task<ChannelQueue> CreatePersistentQueue(this Channel channel,
                                                                     ushort queueId,
                                                                     DeleteWhen deleteWhen,
                                                                     ProducerAckDecision producerAckDecision,
                                                                     ChannelQueueOptions options)
        {
            ChannelQueue queue = await CreateQueue(channel, queueId, deleteWhen, producerAckDecision, options);
            PersistentDeliveryHandler deliveryHandler = (PersistentDeliveryHandler) queue.DeliveryHandler;
            ConfigurationFactory.Manager.Add(queue, deliveryHandler.Database.File.Filename);
            ConfigurationFactory.Manager.Save();
            return queue;
        }

        /// <summary>
        /// Creates and returns queue
        /// </summary>
        internal static async Task<ChannelQueue> CreateQueue(Channel channel,
                                                             ushort queueId,
                                                             DeleteWhen deleteWhen,
                                                             ProducerAckDecision producerAckDecision,
                                                             ChannelQueueOptions options)
        {
            return await channel.CreateQueue(queueId, options, async builder =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
                PersistentDeliveryHandler handler = new PersistentDeliveryHandler(builder.Queue, databaseOptions, deleteWhen, producerAckDecision);
                await handler.Initialize();
                return handler;
            });
        }

        /// <summary>
        /// Generates full file path for database file of the queue
        /// </summary>
        private static string DefaultQueueDbPath(ChannelQueue queue)
        {
            string dir = "data/" + queue.Channel.Name;
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                return dir + "/" + queue.Id + ".tdb";
            }
            catch
            {
                return "data-" + queue.Channel.Name + "-" + queue.Id + ".tdb";
            }
        }
    }
}