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
		int _initialOutputRequest = -1;

		public FrameHandlerBase(RSocket socket)
		{
			this.Socket = socket;
		}
		public FrameHandlerBase(RSocket socket, int streamId) : this(socket)
		{
			this.StreamId = streamId;

			this.OutputCts.Token.Register(this.StopOutging);
		}

		public RSocket Socket { get; set; }
		public int StreamId { get; set; }
		public CancellationTokenSource OutputCts { get; set; } = new CancellationTokenSource();

		public IObserver<Payload> IncomingReceiver { get; set; }
		public IObserver<int> RequestNReceiver { get; set; }
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
			this.RequestNReceiver?.OnCompleted();
			this.OutputSubscription?.Dispose();
		}

		void Request(IDisposable subscription, int requestNumber)
		{
			ISubscription sub = subscription as ISubscription;
			sub?.Request(requestNumber);
		}
		protected virtual void OnSubscribeOutputStream(IDisposable subscription)
		{
			//trigger generate output data.
			this.Request(subscription, this._initialOutputRequest);
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
			bool isFirstRequestN = Interlocked.CompareExchange(ref this._initialOutputRequest, message.RequestNumber, -1) == -1;

			var handler = this.GetRequestNHandler();

#if DEBUG
			if (handler == null)
				Console.WriteLine("missing reuqest(n) handler");
#endif

			if (handler != null)
			{
				handler.OnNext(message.RequestNumber);
				if (!isFirstRequestN)
					this.NotifyOutputPublisher(message.RequestNumber);
			}
		}

		protected virtual void NotifyOutputPublisher(int requestNumber)
		{
			this.Request(this.OutputSubscription, requestNumber);
		}

		protected virtual IObserver<int> GetRequestNHandler()
		{
			return this.RequestNReceiver;
		}

		public virtual void HandleCancel(RSocketProtocol.Cancel message)
		{
#if DEBUG
			Console.WriteLine($"this.OutputCts.Cancel() {this.OutputCts.Token.IsCancellationRequested}");
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
		protected virtual IObservable<int> GetRequestNObservable()
		{
			throw new NotImplementedException();
		}

		protected virtual async Task CreateTask()
		{
			var outgoing = this.GetOutgoing();

			var outputStream = Observable.Create<Payload>(observer =>
			{
				this.OutputSubscriber = observer;
				this.OutputSubscription = outgoing.Subscribe(observer);
				this.OnSubscribeOutputStream(this.OutputSubscription);

				return Disposable.Empty;
			});

			var requestNObservable = this.GetRequestNObservable();

			var outputPayloads = Helpers.MakeControllableStream(outputStream, requestNObservable);

			var outputTask = Helpers.ForEach(outputPayloads,
				action: async value =>
				{
					await new RSocketProtocol.Payload(this.StreamId, value.Data, value.Metadata, next: true).WriteFlush(this.Socket.Transport.Output, value.Data, value.Metadata);
				},
				final: async () =>
				{
					await new RSocketProtocol.Payload(this.StreamId, complete: true).WriteFlush(this.Socket.Transport.Output);
					this.RequestNReceiver.OnCompleted();
				}, cancel: this.OutputCts.Token);

			await outputTask;
		}
		protected virtual void OnTaskCreated()
		{

		}

		public virtual async Task ToTask()
		{
			var outputTask = this.CreateTask();
			this.OnTaskCreated();
			await outputTask;
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
