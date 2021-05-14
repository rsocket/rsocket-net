using System;

namespace RSocket
{
	public interface IPublisher<T> : IObservable<T>
	{
		new ISubscription Subscribe(IObserver<T> observer);
	}
}
