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
	internal class RequesterIncomingStream : IObservable<PayloadContent>
	{
		RSocket Socket;
		IObservable<PayloadContent> _outputs;
		Func<int, Task> _channelBuilder;

		public RequesterIncomingStream(RSocket socket, IObservable<PayloadContent> outputs, Func<int, Task> channelBuilder)
		{
			this.Socket = socket;
			this._outputs = outputs;
			this._channelBuilder = channelBuilder;
		}

		public IDisposable Subscribe(IObserver<PayloadContent> observer)
		{
			var streamId = this.Socket.NewStreamId();

			var inc = Observable.Create<PayloadContent>(observer =>
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
						frameHandlerTask = frameHandler.AsTask();

						this.OnSubscribe(streamId, frameHandler); //TODO handle error

						await this._channelBuilder(streamId).ConfigureAwait(false); //TODO handle error
						await Task.WhenAll(frameHandlerTask, incomingTask);
					}
					finally
					{
						this.Socket.FrameHandlerRemove(streamId);
						frameHandler.Dispose();

#if DEBUG
						Console.WriteLine("Requester.frameHandler.Dispose()");
#endif
					}
				});

				return () =>
				{
					var setResult = incomingTaskSignal.TrySetResult(true);

#if DEBUG
					Task.Run(() =>
					{
						Thread.Sleep(2000);
						Console.WriteLine($"incomingTask.Status:{incomingTask.Status},frameHandlerTask.Status:{frameHandlerTask.Status}");
					});
#endif
				};
			});

			var subscription = new IncomingStreamWrapper<PayloadContent>(inc, this.Socket, streamId).Subscribe(observer);
			return subscription;
		}

		protected virtual void OnSubscribe(int streamId, IFrameHandler frameHandler)
		{

		}
	}

}
