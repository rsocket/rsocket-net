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
	internal class StreamReceiver : IObserver<PayloadContent>
	{
		ISubscriber<PayloadContent> _subscriber;
		IObserver<PayloadContent> _stream;
		public StreamReceiver(ISubscriber<PayloadContent> subscriber, IObserver<PayloadContent> stream)
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
		public void OnNext(PayloadContent value)
		{
			//if(complete)

			this._subscriber.OnNext(value); //TODO: handle error
			this._stream.OnNext(value);
		}
	}
}
