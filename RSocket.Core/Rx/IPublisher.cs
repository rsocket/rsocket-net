using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	public interface IPublisher<T> : IObservable<T>
	{
		new ISubscription Subscribe(IObserver<T> observer);
	}
}
