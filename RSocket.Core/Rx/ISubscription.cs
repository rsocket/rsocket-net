using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	public interface ISubscription : IDisposable
	{
		void Request(int n);
	}

	internal class Subscription : ISubscription
	{
		int _streamId;
		PipeWriter _pipe;

		bool _canceled = false;
		bool _completed = false;
		bool _disposed = false;

		public Subscription(int streamId, PipeWriter pipe)
		{
			this._streamId = streamId;
			this._pipe = pipe;
		}

		public IDisposable RxSubscription { get; set; }

		public void SetCompleted()
		{
			this._completed = true;
		}

		public void Request(int n)
		{
			if (this._canceled)
				return;

			var requestne = new RequestN(this._streamId, default(ReadOnlySequence<byte>), initialRequest: n);
			requestne.WriteFlush(this._pipe);
		}

		public void Cancel()
		{
			if (this._canceled)
				return;

			this.RxSubscription?.Dispose();

			if (!this._completed)
			{
				var cancel = new Cancel(this._streamId);
				cancel.WriteFlush(this._pipe).GetAwaiter().GetResult();
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
