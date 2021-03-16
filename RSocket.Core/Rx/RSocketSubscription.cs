using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	class RSocketSubscription<T> : ISubscription
	{
		int _streamId;
		PipeWriter _pipe;

		bool _canceled = false;
		bool _disposed = false;

		public RSocketSubscription(IDisposable rxSubscription, RSocketObserver<T> observer, int streamId, PipeWriter pipe)
		{
			this.RxSubscription = rxSubscription;
			this.Observer = observer;
			this._streamId = streamId;
			this._pipe = pipe;
		}

		public RSocketObserver<T> Observer { get; set; }
		public IDisposable RxSubscription { get; set; }

		public void Request(int n)
		{
			if (this._canceled)
				return;

			if (this.Observer.IsCompleted)
			{
				return;
			}

			var requestne = new RequestN(this._streamId, default(ReadOnlySequence<byte>), initialRequest: n);
			requestne.WriteFlush(this._pipe);
		}

		void Cancel()
		{
			if (this._canceled)
				return;

			this.RxSubscription?.Dispose();

			if (!this.Observer.IsCompleted)
			{
#if DEBUG
				Console.WriteLine("sending cancel frame");
#endif

				var cancel = new Cancel(this._streamId);
				cancel.WriteFlush(this._pipe).GetAwaiter().GetResult(); //TODO handle errors.
			}

			this._canceled = true;
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this.Cancel();
			this.Dispose(true);

			this._disposed = true;
		}

		protected virtual void Dispose(bool dispoing)
		{

		}
	}
}
