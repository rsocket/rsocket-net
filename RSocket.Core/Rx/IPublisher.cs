using System;

namespace RSocket
{
	public interface IPublisher<T> : IObservable<T>
	{
		new ISubscription Subscribe(IObserver<T> observer);
	}

	public class Publisher<T> : IPublisher<T>
	{
		IObservable<T> _source;

		public Publisher(IObservable<T> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			this._source = source;
		}

		public ISubscription Subscribe(IObserver<T> observer)
		{
			if (observer == null)
				throw new ArgumentNullException(nameof(observer));

			Subscription sub = new Subscription(this._source.Subscribe(observer));
			return sub;
		}

		IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
		{
			return (this as IPublisher<T>).Subscribe(observer);
		}
	}
}
