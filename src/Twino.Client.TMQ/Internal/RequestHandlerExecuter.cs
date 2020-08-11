using System;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
	internal class RequestHandlerExecuter<TRequest, TResponse> : ConsumerExecuter
	{
		private readonly Type _handlerType;
		private readonly ITwinoRequestHandler<TRequest, TResponse> _handler;
		private readonly Func<IConsumerFactory> _handlerFactoryCreator;

		public RequestHandlerExecuter(Type handlerType, ITwinoRequestHandler<TRequest, TResponse> handler, Func<IConsumerFactory> handlerFactoryCreator)
		{
			_handlerType = handlerType;
			_handler = handler;
			_handlerFactoryCreator = handlerFactoryCreator;
			ResolveAttributes(_handlerType, typeof(TRequest));
		}

		public override async Task Execute(TmqClient client, TmqMessage message, object model)
		{
			TRequest requestModel = (TRequest) model;
			Exception exception = null;
			IConsumerFactory consumerFactory = null;

			try
			{
				ITwinoRequestHandler<TRequest, TResponse> handler;

				if (_handler != null)
					handler = _handler;
				else if (_handlerFactoryCreator != null)
				{
					consumerFactory = _handlerFactoryCreator();
					object consumerObject = await consumerFactory.CreateConsumer(_handlerType);
					handler = (ITwinoRequestHandler<TRequest, TResponse>) consumerObject;
				}
				else
					throw new ArgumentNullException("There is no consumer defined");

				try
				{
					TResponse responseModel = await handler.Handle(requestModel, message, client);
					TwinoResultCode code = responseModel is null ? TwinoResultCode.NoContent : TwinoResultCode.Ok;
					TmqMessage responseMessage = message.CreateResponse(code);
					if (requestModel != null)
						responseMessage.Serialize(responseModel, client.JsonSerializer);
					await client.SendAsync(responseMessage);
				}
				catch (Exception e)
				{
					ErrorResponse errorModel = await handler.OnError(e, requestModel, message, client);
					if (errorModel.ResultCode == TwinoResultCode.Ok)
						errorModel.ResultCode = TwinoResultCode.Failed;

					TmqMessage responseMessage = message.CreateResponse(errorModel.ResultCode);

					if (!string.IsNullOrEmpty(errorModel.Reason))
						responseMessage.SetStringContent(errorModel.Reason);

					await client.SendAsync(responseMessage);
				}
			}
			catch (Exception e)
			{
				Type exceptionType = e.GetType();
				var kv = PushExceptions.ContainsKey(exceptionType)
					? PushExceptions[exceptionType]
					: DefaultPushException;

				if (!string.IsNullOrEmpty(kv.Key))
				{
					string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(e);
					await client.Queues.Push(kv.Key, kv.Value, serialized, false);
				}

				exception = e;
				throw;
			}
			finally
			{
				if (consumerFactory != null)
					consumerFactory.Consumed(exception);
			}
		}
	}
}