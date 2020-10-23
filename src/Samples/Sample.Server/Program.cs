﻿using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Queues;
using Twino.Server;

namespace Sample.Server
{
    class Program
    {
        static Task Main(string[] args)
        {
            TwinoMQ mq = TwinoMqBuilder.Create()
                                       .AddOptions(o => o.Status = QueueStatus.Broadcast)
                                       .AddClientHandler<ClientHandler>()
                                       .AddQueueEventHandler<QueueEventHandler>()
                                       .AddPersistentQueues()
                                       .UsePersistentDeliveryHandler(DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterSaved)
                                       .UseJustAllowDeliveryHandler()
                                       .Build();

            mq.LoadPersistentQueues();

            TwinoServer server = new TwinoServer();
            server.UseTwinoMQ(mq);
            server.Start(26222);

            return server.BlockWhileRunningAsync();
        }
    }
}