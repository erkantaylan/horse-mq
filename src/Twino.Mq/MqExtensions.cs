using Twino.MQ.Network;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Twino.MQ
{
    /// <summary>
    /// Extension Methods for Twino.Mq
    /// </summary>
    public static class MqExtensions
    {
        /// <summary>
        /// Uses Twino.Mq Messaging Queue server
        /// </summary>
        public static TwinoServer UseMqServer(this TwinoServer server, MqServer mqServer)
        {
            NetworkMessageHandler handler = new NetworkMessageHandler(mqServer);
            mqServer.Server = server;

            mqServer.NodeManager.ConnectionHandler = new NodeConnectionHandler(mqServer.NodeManager, handler);
            server.UseTmq(handler);

            if (mqServer.NodeManager != null)
                mqServer.NodeManager.SubscribeStartStop(server);

            return server;
        }
    }
}