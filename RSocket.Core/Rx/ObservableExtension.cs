using RSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	public static class ObservableExtension
	{
		public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action onCompleted, Action<Exception> onError, Action<ISubscription> onSubscribe)
		{
			return source.Subscribe(new InnerSubscriber<T>(onNext, onError, onCompleted, onSubscribe));
		}
	}
}
