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

			var inc = Observable.Create<Payload>(observer =>
			{
				RequesterFrameHandler frameHandler = new RequesterFrameHandler(this.Socket, streamId, observer, this._outputs);
				this.Socket.FrameHandlerDispatch(streamId, frameHandler);

				TaskCompletionSource<bool> incomingTaskSignal = new TaskCompletionSource<bool>();
				Task incomingTask = null;
				Task frameHandlerTask = null;

				this.Socket.Schedule(streamId, async (stream, cancel) =>
				{
					try
					{
						incomingTask = incomingTaskSignal.Task;
						frameHandlerTask = frameHandler.ToTask();

						this.OnSubscribe(streamId, frameHandler); //TODO handle error

						await this._channelEstablisher(streamId).ConfigureAwait(false); //TODO handle error
						await Task.WhenAll(frameHandlerTask, incomingTask);

#if DEBUG
						Console.WriteLine($"Requester task status: incomingTask.Status [{streamId}]:{incomingTask.Status},frameHandlerTask.Status:{frameHandlerTask.Status}");
#endif
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
					var setResult = incomingTaskSignal.TrySetResult(true);

#if DEBUG
					Console.WriteLine($"Requester task status: incomingTask.Status [{streamId}]:{incomingTask.Status},frameHandlerTask.Status:{frameHandlerTask.Status}");
#endif
				};
			});

			var subscription = (new IncomingPublisher<Payload>(inc, this.Socket, streamId) as IPublisher<Payload>).Subscribe(observer);
			return subscription;
		}

		protected virtual void OnSubscribe(int streamId, IFrameHandler frameHandler)
		{

		}
	}
}
