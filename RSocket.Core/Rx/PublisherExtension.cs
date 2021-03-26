using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

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

		public static IPublisher<T> ObserveOn<T>(this IPublisher<T> source, SynchronizationContext context)
		{
			return new PublisherAdapter<T>(source, observable =>
			{
				return observable.ObserveOn(context);
			});
		}
		public static IPublisher<T> ObserveOn<T>(this IPublisher<T> source, IScheduler scheduler)
		{
			return new PublisherAdapter<T>(source, observable =>
			{
				return observable.ObserveOn(scheduler);
			});
		}

		public static IPublisher<T> SubscribeOn<T>(this IPublisher<T> source, SynchronizationContext context)
		{
			return new PublisherAdapter<T>(source, observable =>
			{
				return observable.SubscribeOn(context);
			});
		}
		public static IPublisher<T> SubscribeOn<T>(this IPublisher<T> source, IScheduler scheduler)
		{
			return new PublisherAdapter<T>(source, observable =>
			{
				return observable.SubscribeOn(scheduler);
			});
		}

		static IObservable<T> AsIObservable<T>(this IPublisher<T> source)
		{
			return source;
		}


		class PublisherAdapter<T> : IPublisher<T>
		{
			IPublisher<T> _source;
			Func<IObservable<T>, IObservable<T>> _adapter;

			public PublisherAdapter(IPublisher<T> source, Func<IObservable<T>, IObservable<T>> _adapter)
			{
				this._source = source;
				this._adapter = _adapter;
			}

			public ISubscription Subscribe(IObserver<T> observer)
			{
				Subject<T> subject = new Subject<T>();

				var rxSub = this._adapter(subject).Subscribe(observer);
				ISubscription sub = this._source.Subscribe(subject);

				return new Subscription(sub, rxSub);
			}

			IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
			{
				return (this as IPublisher<T>).Subscribe(observer);
			}

			class Subscription : ISubscription
			{
				ISubscription _subscription;
				IDisposable _rxSubscription;

				public Subscription(ISubscription subscription, IDisposable rxSubscription)
				{
					this._subscription = subscription;
					this._rxSubscription = rxSubscription;
				}

				public void Dispose()
				{
					this._subscription.Dispose();
					this._rxSubscription.Dispose();
				}

				public void Request(int n)
				{
					this._subscription.Request(n);
				}
			}
		}
	}
}
