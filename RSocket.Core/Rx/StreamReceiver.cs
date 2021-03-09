using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using static RSocket.RSocketProtocol;
using IRSocketStream = System.IObserver<(System.Buffers.ReadOnlySequence<byte> metadata, System.Buffers.ReadOnlySequence<byte> data)>;

namespace RSocket
{
	internal class StreamReceiver : IObserver<(System.Buffers.ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>
	{
		ISubscriber<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)> _subscriber;
		IRSocketStream _stream;
		public StreamReceiver(ISubscriber<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)> subscriber, IRSocketStream stream)
		{
			this._subscriber = subscriber;
			this._stream = stream;
		}

		public void OnCompleted()
		{
			this._subscriber.OnCompleted();
			this._stream.OnCompleted();
		}
		public void OnError(Exception error)
		{
			this._subscriber.OnError(error);
			this._stream.OnError(error);
		}
		public void OnNext((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value)
		{
			this._subscriber.OnNext(value);
			this._stream.OnNext(value);
		}
	}
}
