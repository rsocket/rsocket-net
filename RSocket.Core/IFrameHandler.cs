using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static RSocket.RSocketProtocol;
using IRSocketStream = System.IObserver<RSocket.PayloadContent>;

namespace RSocket
{
	public interface IFrameHandler : IDisposable
	{
		void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void HandleRequestN(RSocketProtocol.RequestN message);
		void HandleCancel(RSocketProtocol.Cancel message);
		Task AsTask();
	}

	public class FrameHandlerBase : IFrameHandler
	{
		public FrameHandlerBase(RSocket socket)
		{
			this.Socket = socket;
		}

		public RSocket Socket { get; set; }
		public CancellationTokenSource OutputCts { get; set; } = new CancellationTokenSource();

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
			return null;
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
			}
		}

		protected virtual IObserver<int> GetRequestNHandler()
		{
			return null;
		}

		public virtual void HandleCancel(RSocketProtocol.Cancel message)
		{
			this.OutputCts.Cancel();
#if DEBUG
			Console.WriteLine($"this.OutputCts.Cancel() {this.OutputCts.Token.IsCancellationRequested}");
#endif
		}

		public virtual Task AsTask()
		{
			return Task.CompletedTask;
		}

		public void Dispose()
		{
			if (!this.OutputCts.IsCancellationRequested)
				this.OutputCts.Cancel();

			this.OutputCts.Dispose();

			this.Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{

		}
	}
}
