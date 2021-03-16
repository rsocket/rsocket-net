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

		public FrameHandlerBase(RSocket socket)
		{
			this.Socket = socket;
		}
		public FrameHandlerBase(RSocket socket, int streamId) : this(socket)
		{
			this.StreamId = streamId;
		}

		public RSocket Socket { get; set; }
		public int StreamId { get; set; }
		public CancellationTokenSource OutputCts { get; set; } = new CancellationTokenSource();

		public IObserver<PayloadContent> IncomingReceiver { get; set; }
		public IObserver<int> RequestNReceiver { get; set; }
		public IObserver<PayloadContent> OutputSubscriber { get; set; }
		public IDisposable OutputSubscriberSubscription { get; set; }

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
					handler.OnNext(new PayloadContent(data, metadata));
				}

				if (message.IsComplete)
				{
					handler.OnCompleted();
				}
			}
		}
		protected virtual IObserver<PayloadContent> GetPayloadHandler()
		{
			return this.IncomingReceiver;
		}

		public virtual void HandleRequestN(RSocketProtocol.RequestN message)
		{
			var handler = this.GetRequestNHandler();

#if DEBUG
			if (handler == null)
				Console.WriteLine("missing reuqestn handler");
#endif

			if (handler != null)
			{
				handler.OnNext(message.RequestNumber);
				this.NotifyOutputPublisher(message.RequestNumber);
			}
		}

		protected virtual void NotifyOutputPublisher(int requestNumber)
		{
			ISubscription sub = this.OutputSubscriberSubscription as ISubscription;
			sub?.Request(requestNumber);
		}

		protected virtual IObserver<int> GetRequestNHandler()
		{
			return this.RequestNReceiver;
		}

		public virtual void HandleCancel(RSocketProtocol.Cancel message)
		{
			if (this._disposed)
				return;

			if (!this.OutputCts.IsCancellationRequested)
				this.OutputCts.Cancel(false);
#if DEBUG
			Console.WriteLine($"this.OutputCts.Cancel() {this.OutputCts.Token.IsCancellationRequested}");
#endif
		}

		public virtual Task ToTask()
		{
			return Task.CompletedTask;
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
