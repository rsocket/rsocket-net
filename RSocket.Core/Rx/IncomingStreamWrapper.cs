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
	internal class IncomingStreamWrapper<T> : IObservable<T>
	{
		IObservable<T> _stream;
		RSocket Socket;
		int StreamId;
		public IncomingStreamWrapper(IObservable<T> stream, RSocket socket, int streamId)
		{
			this._stream = stream;
			this.Socket = socket;
			this.StreamId = streamId;
		}

		public IDisposable Subscribe(IObserver<T> observer)
		{
			Subscription subscription = new Subscription(this.StreamId, this.Socket.Transport.Output);
			observer = new ObserverWrapper<T>(observer, subscription);
			var sub = this._stream.Subscribe(observer);
			subscription.RxSubscription = sub;
			return subscription;
		}

		class ObserverWrapper<TData> : IObserver<TData>
		{
			IObserver<TData> _observer;
			Subscription _sub;
			bool _completed = false;
			public ObserverWrapper(IObserver<TData> observer, Subscription sub)
			{
				this._observer = observer;
				this._sub = sub;
			}

			public void OnCompleted()
			{
				if (this._completed)
					return;

				this._completed = true;
				this._sub.SetCompleted();
				this._observer.OnCompleted();
			}

			public void OnError(Exception error)
			{
				this._observer.OnError(error);
			}

			public void OnNext(TData value)
			{
				this._observer.OnNext(value);
			}
		}
	}
}
