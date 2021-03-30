using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RSocket
{
	class SimplePublisher<T> : IPublisher<T>, IObserver<T>, IObservable<T>, IDisposable
	{
		IObserver<T> _subscriber;

		public SimplePublisher()
		{
		}

		IObserver<T> Unsubscribe()
		{
			return Interlocked.Exchange(ref this._subscriber, null);
		}

		public void Dispose()
		{
			this.Unsubscribe();
		}

		public void OnCompleted()
		{
			this.Unsubscribe()?.OnCompleted();
		}

		public void OnError(Exception error)
		{
			this.Unsubscribe()?.OnError(error);
		}

		public void OnNext(T value)
		{
			try
			{
				this._subscriber?.OnNext(value);
			}
			catch
			{
				this.Unsubscribe();
				throw;
			}
		}

		public IDisposable Subscribe(IObserver<T> observer)
		{
			return (this as IPublisher<T>).Subscribe(observer);
		}

		ISubscription IPublisher<T>.Subscribe(IObserver<T> observer)
		{
			this._subscriber = observer;
			return new Subscription(this);
		}
	}

}
