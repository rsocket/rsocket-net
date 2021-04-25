using System;

namespace RSocket
{
	public class PublisherAdapter<T> : IPublisher<T>
	{
		IObservable<T> _source;

		public PublisherAdapter(IObservable<T> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			this._source = source;
		}

		public ISubscription Subscribe(IObserver<T> observer)
		{
			if (observer == null)
				throw new ArgumentNullException(nameof(observer));

			if (this._source is IPublisher<T>)
			{
				return (this._source as IPublisher<T>).Subscribe(observer);
			}

			ISubscription sub = new SubscriptionAdapter(this._source.Subscribe(observer));
			return sub;
		}

		IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
		{
			return (this as IPublisher<T>).Subscribe(observer);
		}
	}
}
