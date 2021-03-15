using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RSocket
{
	class IncomingPublisher<T> : IPublisher<T>, IObservable<T>
	{
		IObservable<T> _stream;
		RSocket Socket;
		int StreamId;
		public IncomingPublisher(IObservable<T> stream, RSocket socket, int streamId)
		{
			this._stream = stream;
			this.Socket = socket;
			this.StreamId = streamId;
		}

		IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
		{
			return (this as IPublisher<T>).Subscribe(observer);
		}

		ISubscription IPublisher<T>.Subscribe(IObserver<T> observer)
		{
			RSocketObserver<T> observerWrapper = new RSocketObserver<T>(observer);
			var sub = this._stream.Subscribe(observerWrapper);
			RSocketSubscription<T> subscription = new RSocketSubscription<T>(sub, observerWrapper, this.StreamId, this.Socket.Transport.Output);
			observerWrapper.Subscription = subscription;
			return subscription;
		}
	}


}
