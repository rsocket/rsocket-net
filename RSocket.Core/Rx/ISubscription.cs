using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	public interface ISubscription
	{
		void Request(int n);
		void Cancel();
	}

	internal class Subscription : ISubscription
	{
		int _streamId;
		PipeWriter _pipe;

		bool _canceled = false;
		public Subscription(int streamId, PipeWriter pipe)
		{
			this._streamId = streamId;
			this._pipe = pipe;
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

			this._canceled = true;
			throw new NotImplementedException();
		}
	}
}
