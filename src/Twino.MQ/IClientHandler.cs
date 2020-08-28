using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ
{
    /// <summary>
    /// Client connect and disconnect event implementations for MqServer
    /// </summary>
    public interface IClientHandler
    {
        /// <summary>
        /// Called when a client is connected and TMQ protocol handshake is completed
        /// </summary>
        Task Connected(TwinoMQ server, MqClient client);

        /// <summary>
        /// Called when a client is disconnected and removed from all queues
        /// </summary>
        Task Disconnected(TwinoMQ server, MqClient client);
    }
}