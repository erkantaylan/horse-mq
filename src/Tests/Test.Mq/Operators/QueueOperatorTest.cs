using System;
using System.Linq;
using System.Threading.Tasks;
using Test.Mq.Internal;
using Test.Mq.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Models;
using Twino.MQ;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;
using Xunit;

namespace Test.Mq.Operators
{
    public class QueueOperatorTest
    {
        /// <summary>
        /// Client sends a queue creation message
        /// </summary>
        [Fact]
        public async Task Create()
        {
            int port = 40905;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            TwinoResult created = await client.CreateQueue("ch-2", MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Ok, created.Code);

            Channel channel = server.Server.Channels.FirstOrDefault(x => x.Name == "ch-2");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.Queues.FirstOrDefault();
            Assert.NotNull(queue);
            Assert.Equal(MessageA.ContentType, queue.Id);
        }

        [Fact]
        public async Task CreateWithProperties()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(21206);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:21206");
            Assert.True(client.IsConnected);

            TwinoResult created = await client.CreateQueue("ch-test", MessageA.ContentType, o =>
            {
                o.SendOnlyFirstAcquirer = true;
                o.AcknowledgeTimeout = 33000;
                o.Status = MessagingQueueStatus.Pull;
            });
            Assert.Equal(TwinoResultCode.Ok, created.Code);

            Channel channel = server.Server.FindChannel("ch-test");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);

            Assert.True(queue.Options.SendOnlyFirstAcquirer);
            Assert.Equal(TimeSpan.FromSeconds(33), queue.Options.AcknowledgeTimeout);
            Assert.Equal(QueueStatus.Pull, queue.Status);
        }

        [Fact]
        public async Task Update()
        {
            TestMqServer server = new TestMqServer();
            server.Initialize(41207);
            server.Start();

            Channel channel = server.Server.FindChannel("ch-route");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);

            Assert.False(queue.Options.WaitForAcknowledge);
            Assert.False(queue.Options.SendOnlyFirstAcquirer);
            Assert.Equal(TimeSpan.FromSeconds(12), queue.Options.MessageTimeout);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:41207");
            Assert.True(client.IsConnected);

            TwinoResult updated = await client.Queues.SetOptions("ch-route", MessageA.ContentType, o =>
            {
                o.WaitForAcknowledge = true;
                o.MessageTimeout = 666000;
                o.SendOnlyFirstAcquirer = true;
            });
            Assert.Equal(TwinoResultCode.Ok, updated.Code);

            Assert.True(queue.Options.WaitForAcknowledge);
            Assert.True(queue.Options.SendOnlyFirstAcquirer);
            Assert.Equal(TimeSpan.FromSeconds(666), queue.Options.MessageTimeout);
        }

        [Fact]
        public async Task Delete()
        {
            int port = 41208;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            Channel channel = server.Server.FindChannel("ch-route");
            Assert.NotNull(channel);

            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            Assert.NotNull(queue);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);
            Assert.True(client.IsConnected);

            TwinoResult done = await client.Queues.Remove("ch-route", MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Ok, done.Code);

            queue = channel.FindQueue(MessageA.ContentType);
            Assert.Null(queue);
        }

        [Fact]
        public async Task GetQueueInfo()
        {
            int port = 41281;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            var queue = await client.Queues.GetInfo("ch-push", MessageA.ContentType);
            Assert.Equal(TwinoResultCode.Ok, queue.Result.Code);
            Assert.NotNull(queue.Model);
            Assert.Equal("ch-push", queue.Model.Channel);
            Assert.Equal(MessageA.ContentType, queue.Model.Id);
        }

        [Fact]
        public async Task GetQueueList()
        {
            int port = 41282;
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            var result = await client.Queues.List("ch-push");
            Assert.Equal(TwinoResultCode.Ok, result.Result.Code);
            Assert.NotNull(result.Model);
            var queue = result.Model.FirstOrDefault();
            Assert.NotNull(queue);
            Assert.Equal(MessageA.ContentType, queue.Id);
        }

        /// <summary>
        /// Clears messages in queue
        /// </summary>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task ClearMessages(bool priorityMessages, bool messages)
        {
            int port = 41282 + Convert.ToInt32(priorityMessages) + (Convert.ToInt32(messages) * 3);
            TestMqServer server = new TestMqServer();
            server.Initialize(port);
            server.Start();

            var channel = server.Server.FindChannel("ch-push");
            ChannelQueue queue = channel.FindQueue(MessageA.ContentType);
            queue.AddStringMessageWithId("Hello, World", false, false);
            queue.AddStringMessageWithId("Hello, World", false, true);

            TmqClient client = new TmqClient();
            await client.ConnectAsync("tmq://localhost:" + port);

            var result = await client.Queues.ClearMessages("ch-push", MessageA.ContentType, priorityMessages, messages);
            Assert.Equal(TwinoResultCode.Ok, result.Code);

            if (priorityMessages)
                Assert.Empty(queue.PriorityMessages);
            else
                Assert.NotEmpty(queue.PriorityMessages);

            if (messages)
                Assert.Empty(queue.Messages);
            else
                Assert.NotEmpty(queue.Messages);
        }
    }
}