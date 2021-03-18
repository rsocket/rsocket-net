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
	public class FrameHandlerBase : IFrameHandler
	{
		bool _disposed = false;
		int _initialOutputRequest = 0;

		Task _outputTask;

		public FrameHandlerBase(RSocket socket)
		{
			this.Socket = socket;
		}
		public FrameHandlerBase(RSocket socket, int streamId) : this(socket)
		{
			this.StreamId = streamId;

			this.OutputCts.Token.Register(this.StopOutging);
		}

		public FrameHandlerBase(RSocket socket, int streamId, int initialOutputRequest) : this(socket)
		{
			this.StreamId = streamId;
			this._initialOutputRequest = initialOutputRequest;

			this.OutputCts.Token.Register(this.StopOutging);
		}

		public RSocket Socket { get; set; }
		public int StreamId { get; set; }
		public CancellationTokenSource OutputCts { get; set; } = new CancellationTokenSource();

		public IObserver<Payload> IncomingReceiver { get; set; }
		public IObserver<Payload> OutputSubscriber { get; set; }
		public IDisposable OutputSubscription { get; set; }

		protected virtual void StopIncoming()
		{
			this.IncomingReceiver?.OnCompleted();
		}
		protected virtual void StopOutging()
		{
			//cancel sending payload.
			this.OutputSubscriber?.OnCompleted();
		}

		void ExecuteRequest(IDisposable subscription, int requestNumber)
		{
			ISubscription sub = subscription as ISubscription;
			sub?.Request(requestNumber);
		}
		protected virtual void OnSubscribeOutputStream(IDisposable subscription)
		{
			//trigger generate output data.
			if (this._initialOutputRequest > 0)
				this.ExecuteRequest(subscription, this._initialOutputRequest);
		}

		public virtual void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			var handler = this.GetPayloadHandler();

#if DEBUG
			if (handler == null)
				Console.WriteLine("missing payload handler");
#endif

			if (handler != null)
			{
				if (message.IsNext)
				{
					handler.OnNext(new Payload(data, metadata));
				}

				if (message.IsComplete)
				{
					handler.OnCompleted();
				}
			}
		}
		protected virtual IObserver<Payload> GetPayloadHandler()
		{
			return this.IncomingReceiver;
		}

		public virtual void HandleRequestN(RSocketProtocol.RequestN message)
		{
			this.NotifyOutputPublisher(message.RequestNumber);
		}

		void NotifyOutputPublisher(int requestNumber)
		{
			this.ExecuteRequest(this.OutputSubscription, requestNumber);
		}

		public virtual void HandleCancel(RSocketProtocol.Cancel message)
		{
#if DEBUG
			Console.WriteLine($"frameHandler.HandleCancel() {this.OutputCts.Token.IsCancellationRequested}");
#endif

			if (this._disposed)
				return;

			if (!this.OutputCts.IsCancellationRequested)
				this.OutputCts.Cancel(false);
		}

		protected virtual IObservable<Payload> GetOutgoing()
		{
			throw new NotImplementedException();
		}

		protected virtual Task GetInputTask()
		{
			return Task.CompletedTask;
		}
		protected virtual Task GetOutputTask()
		{
			return this._outputTask;
		}

		protected virtual async Task CreateTask()
		{
			var outputTask = this.GetOutputTask();
			var inputTask = this.GetInputTask();
			await Task.WhenAll(outputTask, inputTask);
		}

		protected virtual void OnTaskCreating()
		{
			var outgoing = this.GetOutgoing();

			var outputStream = Observable.Create<Payload>(observer =>
			{
				this.OutputSubscriber = observer;
				this.OutputSubscription = outgoing.Subscribe(observer);
				this.OnSubscribeOutputStream(this.OutputSubscription);

				return () =>
				{
					this.OutputSubscription.Dispose();
				};
			});

			var outputTask = Helpers.ForEach(outputStream.ToAsyncEnumerable(),
				action: async value =>
				{
					await new RSocketProtocol.Payload(this.StreamId, value.Data, value.Metadata, next: true).WriteFlush(this.Socket.Transport.Output, value.Data, value.Metadata);
				},
				final: async () =>
				{
					await new RSocketProtocol.Payload(this.StreamId, complete: true).WriteFlush(this.Socket.Transport.Output);
				}, cancel: this.OutputCts.Token);

			this._outputTask = outputTask;
		}
		protected virtual void OnTaskCreated()
		{

		}

		public virtual async Task ToTask()
		{
			this.OnTaskCreating();
			var task = this.CreateTask();
			this.OnTaskCreated();
			await task;
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			if (!this.OutputCts.IsCancellationRequested)
				this.OutputCts.Cancel();

			this.OutputCts.Dispose();

			this.Dispose(true);

			this._disposed = true;
		}

		protected virtual void Dispose(bool disposing)
		{

		}
	}
}
