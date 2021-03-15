using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	public static class PublisherExtension
	{
		public static ISubscription Subscribe<T>(this IPublisher<T> source, Action<T> onNext)
		{
			return (ISubscription)source.AsIObservable().Subscribe(onNext);
		}
		public static ISubscription Subscribe<T>(this IPublisher<T> source, Action<T> onNext, Action<Exception> onError)
		{
			return (ISubscription)source.AsIObservable().Subscribe(onNext, onError);
		}
		public static ISubscription Subscribe<T>(this IPublisher<T> source, Action<T> onNext, Action onCompleted)
		{
			return (ISubscription)source.AsIObservable().Subscribe(onNext, onCompleted);
		}
		public static ISubscription Subscribe<T>(this IPublisher<T> source, Action<T> onNext, Action<Exception> onError, Action onCompleted)
		{
			return (ISubscription)source.AsIObservable().Subscribe(onNext, onError, onCompleted);
		}

		public static IObservable<T> AsIObservable<T>(this IPublisher<T> source)
		{
			return source;
		}
	}
}
