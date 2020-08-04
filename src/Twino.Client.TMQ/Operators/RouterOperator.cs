using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations.Resolvers;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Operators
{
    /// <summary>
    /// Router manager object for tmq client
    /// </summary>
    public class RouterOperator
    {
        private readonly TmqClient _client;

        internal RouterOperator(TmqClient client)
        {
            _client = client;
        }

        #region Actions

        //todo: create
        //todo: list
        //todo: remove
        
        //todo: add binding
        //todo: get bindings
        //todo: remove binding
        
        #endregion

        #region Publish

        /// <summary>
        /// Publishes a string message to a router
        /// </summary>
        public async Task<TwinoResult> Publish(string routerName, string message, bool waitForAcknowledge = false, ushort contentType = 0)
        {
            TmqMessage msg = new TmqMessage(MessageType.Router, routerName, contentType);
            msg.PendingAcknowledge = waitForAcknowledge;
            msg.SetMessageId(_client.UniqueIdGenerator.Create());
            msg.Content = new MemoryStream(Encoding.UTF8.GetBytes(message));

            return await _client.SendAndWaitForAcknowledge(msg, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a byte array data to a router
        /// </summary>
        public async Task<TwinoResult> Publish(string routerName, byte[] data, bool waitForAcknowledge = false, ushort contentType = 0)
        {
            TmqMessage msg = new TmqMessage(MessageType.Router, routerName, contentType);
            msg.PendingAcknowledge = waitForAcknowledge;
            msg.SetMessageId(_client.UniqueIdGenerator.Create());
            msg.Content = new MemoryStream(data);

            return await _client.SendAndWaitForAcknowledge(msg, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public Task<TwinoResult> PublishJson<TModel>(TModel model, bool waitForAcknowledge = false)
        {
            return PublishJson(null, model, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public async Task<TwinoResult> PublishJson(string routerName, object model, bool waitForAcknowledge = false, ushort? contentType = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor(model.GetType());
            TmqMessage message = descriptor.CreateMessage(MessageType.Router, routerName, contentType);

            message.PendingAcknowledge = waitForAcknowledge;
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            message.Serialize(model, _client.JsonSerializer);
            
            return await _client.SendAndWaitForAcknowledge(message, waitForAcknowledge);
        }

        /// <summary>
        /// Sends a string request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public async Task<TmqMessage> PublishRequest(string routerName, string message, ushort contentType = 0)
        {
            TmqMessage msg = new TmqMessage(MessageType.Router, routerName, contentType);
            msg.PendingResponse = true;
            msg.Content = new MemoryStream(Encoding.UTF8.GetBytes(message));
            return await _client.Request(msg);
        }

        /// <summary>
        /// Sends a request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(TRequest request)
        {
            return PublishRequestJson<TRequest, TResponse>(null, request);
        }

        /// <summary>
        /// Sends a request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public async Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(string routerName, TRequest request, ushort? contentType = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor<TRequest>();
            TmqMessage message = descriptor.CreateMessage(MessageType.Router, routerName, contentType);
            message.PendingResponse = true;
            message.Serialize(request, _client.JsonSerializer);

            TmqMessage responseMessage = await _client.Request(message);
            if (responseMessage.ContentType == 0)
            {
                TResponse response = responseMessage.Deserialize<TResponse>(_client.JsonSerializer);
                return new TwinoResult<TResponse>(response, message, TwinoResultCode.Ok);
            }

            return new TwinoResult<TResponse>(default, responseMessage, (TwinoResultCode) responseMessage.ContentType);
        }

        #endregion
    }
}