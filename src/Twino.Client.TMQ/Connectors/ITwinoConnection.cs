using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Base Twino Connection implementation
    /// </summary>
    public interface ITwinoConnection
    {
        /// <summary>
        /// Sends a raw message
        /// </summary>
        Task<TwinoResult> SendAsync(TmqMessage message);

        /// <summary>
        /// Sends a raw message and waits for it's response
        /// </summary>
        /// <param name="message">Raw message</param>
        /// <returns>Response message</returns>
        Task<TmqMessage> RequestAsync(TmqMessage message);
        
        /// <summary>
        /// Gets connected client object
        /// </summary>
        TmqClient GetClient();
    }
}