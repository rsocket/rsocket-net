using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	class RequesterIncomingStream : IPublisher<Payload>, IObservable<Payload>
	{
		RSocket Socket;
		IObservable<Payload> _outputs;
		Func<int, Task> _channelEstablisher;

		public RequesterIncomingStream(RSocket socket, IObservable<Payload> outputs, Func<int, Task> channelEstablisher)
		{
			this.Socket = socket;
			this._outputs = outputs;
			this._channelEstablisher = channelEstablisher;
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> observer)
		{
			return (this as IPublisher<Payload>).Subscribe(observer);
		}

		ISubscription IPublisher<Payload>.Subscribe(IObserver<Payload> observer)
		{
			var streamId = this.Socket.NewStreamId();

			RequesterFrameHandler frameHandler = new RequesterFrameHandler(this.Socket, streamId, null, this._outputs);
			var inc = Observable.Create<Payload>(observer =>
			{
				frameHandler.InboundSubscriber = observer;
				this.Socket.FrameHandlerDispatch(streamId, frameHandler);

				this.Socket.Schedule(streamId, async (stream, cancel) =>
				{
					try
					{
						Task frameHandlerTask = frameHandler.ToTask();

						this.OnSubscribe(streamId, frameHandler); //TODO handle error

						await this._channelEstablisher(streamId).ConfigureAwait(false); //TODO handle error

						await frameHandlerTask;
					}
					finally
					{
						this.Socket.FrameHandlerRemove(streamId);
						frameHandler.Dispose();

#if DEBUG
						Console.WriteLine($"Requester.frameHandler.Dispose(): stream[{streamId}]");
#endif
					}
				});

				return () =>
				{
					var setResult = frameHandler.InboundTaskSignal.TrySetResult(true);
				};
			});

			var subscription = (new IncomingPublisher<Payload>(inc, frameHandler) as IPublisher<Payload>).Subscribe(observer);
			return subscription;
		}

		protected virtual void OnSubscribe(int streamId, IFrameHandler frameHandler)
		{

		}
	}
}
