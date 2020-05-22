using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    /// <summary>
    /// Message queue server handler
    /// </summary>
    internal class NetworkMessageHandler : IProtocolConnectionHandler<TmqServerSocket, TmqMessage>
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        private readonly INetworkMessageHandler _serverHandler;
        private readonly INetworkMessageHandler _queueMessageHandler;
        private readonly INetworkMessageHandler _routerMessageHandler;
        private readonly INetworkMessageHandler _pullRequestHandler;
        private readonly INetworkMessageHandler _clientHandler;
        private readonly INetworkMessageHandler _responseHandler;
        private readonly INetworkMessageHandler _acknowledgeHandler;
        private readonly INetworkMessageHandler _instanceHandler;

        public NetworkMessageHandler(MqServer server)
        {
            _server = server;
            _serverHandler = new ServerMessageHandler(server);
            _queueMessageHandler = new QueueMessageHandler(server);
            _routerMessageHandler = new RouterMessageHandler(server);
            _pullRequestHandler = new PullRequestMessageHandler(server);
            _clientHandler = new DirectMessageHandler(server);
            _responseHandler = new ResponseMessageHandler(server);
            _acknowledgeHandler = new AcknowledgeMessageHandler(server);
            _instanceHandler = new NodeMessageHandler(server);
        }

        #endregion

        #region Connection

        /// <summary>
        /// Called when a new client is connected via TMQ protocol
        /// </summary>
        public async Task<TmqServerSocket> Connected(ITwinoServer server, IConnectionInfo connection, ConnectionData data)
        {
            string clientId;
            bool found = data.Properties.TryGetValue(TmqHeaders.CLIENT_ID, out clientId);
            if (!found || string.IsNullOrEmpty(clientId))
                clientId = _server.ClientIdGenerator.Create();

            //if another client with same unique id is online, do not accept new client
            MqClient foundClient = _server.FindClient(clientId);
            if (foundClient != null)
            {
                await connection.Socket.SendAsync(TmqWriter.Create(MessageBuilder.Busy()));
                return null;
            }

            if (_server.Options.ClientLimit > 0 && _server.GetOnlineClients() >= _server.Options.ClientLimit)
                return null;

            //creates new mq client object 
            MqClient client = new MqClient(_server, connection, _server.MessageIdGenerator, _server.Options.UseMessageId);
            client.Data = data;
            client.UniqueId = clientId.Trim();
            client.Token = data.Properties.GetStringValue(TmqHeaders.CLIENT_TOKEN);
            client.Name = data.Properties.GetStringValue(TmqHeaders.CLIENT_NAME);
            client.Type = data.Properties.GetStringValue(TmqHeaders.CLIENT_TYPE);

            //authenticates client
            if (_server.Authenticator != null)
            {
                client.IsAuthenticated = await _server.Authenticator.Authenticate(_server, client);
                if (!client.IsAuthenticated)
                {
                    await client.SendAsync(MessageBuilder.Unauthorized());
                    return null;
                }
            }

            //client authenticated, add it into the connected clients list
            _server.AddClient(client);

            //send response message to the client, client should check unique id,
            //if client's unique id isn't permitted, server will create new id for client and send it as response
            await client.SendAsync(MessageBuilder.Accepted(client.UniqueId));

            if (_server.ClientHandler != null)
                await _server.ClientHandler.Connected(_server, client);

            return client;
        }

        /// <summary>
        /// Triggered when handshake is completed and the connection is ready to communicate 
        /// </summary>
        public Task Ready(ITwinoServer server, TmqServerSocket client)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when connected client is connected in TMQ protocol
        /// </summary>
        public async Task Disconnected(ITwinoServer server, TmqServerSocket client)
        {
            MqClient mqClient = (MqClient) client;
            await _server.RemoveClient(mqClient);
            if (_server.ClientHandler != null)
                await _server.ClientHandler.Disconnected(_server, mqClient);
        }

        #endregion

        #region Receive

        /// <summary>
        /// Called when a new message received from the client
        /// </summary>
        public Task Received(ITwinoServer server, IConnectionInfo info, TmqServerSocket client, TmqMessage message)
        {
            MqClient mc = (MqClient) client;

            //if client sends anonymous messages and server needs message id, generate new
            if (string.IsNullOrEmpty(message.MessageId))
            {
                //anonymous messages can't be responsed, do not wait response
                if (message.PendingResponse)
                    message.PendingResponse = false;

                //if server want to use message id anyway, generate new.
                if (_server.Options.UseMessageId)
                    message.SetMessageId(_server.MessageIdGenerator.Create());
            }

            //if message does not have a source information, source will be set to sender's unique id
            if (string.IsNullOrEmpty(message.Source))
                message.SetSource(mc.UniqueId);

            //if client sending messages like someone another, kick him
            else if (message.Source != mc.UniqueId)
            {
                client.Disconnect();
                return Task.CompletedTask;
            }

            return RouteToHandler(mc, message, false);
        }

        /// <summary>
        /// Routes message to it's type handler
        /// </summary>
        internal Task RouteToHandler(MqClient mc, TmqMessage message, bool fromNode)
        {
            switch (message.Type)
            {
                //client sends a queue message in a channel
                case MessageType.QueueMessage:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message);
                    return _queueMessageHandler.Handle(mc, message);

                case MessageType.Router:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message);
                    return _routerMessageHandler.Handle(mc, message);

                //sends pull request to a queue
                case MessageType.QueuePullRequest:
                    return _pullRequestHandler.Handle(mc, message);

                //clients sends a message to another client
                case MessageType.DirectMessage:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message);
                    return _clientHandler.Handle(mc, message);

                //client sends an acknowledge message of a message
                case MessageType.Acknowledge:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message);

                    return _acknowledgeHandler.Handle(mc, message);

                //client sends a response message for a message
                case MessageType.Response:
                    if (!fromNode)
                        _ = _instanceHandler.Handle(mc, message);
                    return _responseHandler.Handle(mc, message);

                //client sends a message to the server
                //this message may be join, header, info, some another server message
                case MessageType.Server:
                    return _serverHandler.Handle(mc, message);

                //if client sends a ping message, response with pong
                case MessageType.Ping:
                    return mc.SendAsync(PredefinedMessages.PONG);

                //client sends PONG message
                case MessageType.Pong:
                    mc.KeepAlive();
                    break;

                //close the client's connection
                case MessageType.Terminate:
                    mc.Disconnect();
                    break;
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}