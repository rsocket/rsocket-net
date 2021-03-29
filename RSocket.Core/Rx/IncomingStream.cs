using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{

	class IncomingStream : IPublisher<Payload>
	{
		IObservable<Payload> _source;
		FrameHandler _frameHandler;

		int _subscribedFlag = 0;
		bool IsSubscribed
		{
			get
			{
				return Interlocked.CompareExchange(ref this._subscribedFlag, 1, 0) != 0;
			}
		}

		public IncomingStream(IObservable<Payload> source, FrameHandler frameHandler)
		{
			this._source = source;
			this._frameHandler = frameHandler;
		}

		public ISubscription Subscribe(IObserver<Payload> observer)
		{
			if (this.IsSubscribed)
				throw new InvalidOperationException("Incoming stream allows only one Subscriber");

			IncomingStreamSubscriber subscriber = new IncomingStreamSubscriber(observer, this._frameHandler);
			var sub = subscriber.Subscribe(this._source);

			return new IncomingStreamSubscription(sub, this._frameHandler, subscriber);
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> observer)
		{
			return (this as IPublisher<Payload>).Subscribe(observer);
		}

		class IncomingStreamSubscriber : Subscriber<Payload>, IObserver<Payload>, ISubscription
		{
			IObserver<Payload> _observer;
			FrameHandler _frameHandler;

			public IncomingStreamSubscriber(IObserver<Payload> observer, FrameHandler frameHandler)
			{
				this._observer = observer;
				this._frameHandler = frameHandler;
			}

			protected override void DoOnCompleted()
			{
				try
				{
					this._observer.OnCompleted();
				}
				catch
				{
				}

				this._frameHandler.OnIncomingCompleted();
			}

			protected override void DoOnError(Exception error)
			{
				try
				{
					this._observer.OnError(error);
				}
				catch
				{
				}

				this._frameHandler.OnIncomingCompleted();
			}

			protected override void DoOnNext(Payload value)
			{
				try
				{
					this._observer.OnNext(value);
				}
				catch
				{
					this._frameHandler.OnIncomingCanceled();
				}
			}
		}

		class IncomingStreamSubscription : ISubscription
		{
			FrameHandler _frameHandler;
			IncomingStreamSubscriber _subscriber;

			IDisposable _subscription;
			bool _disposed;

			public IncomingStreamSubscription(IDisposable subscription, FrameHandler frameHandler, IncomingStreamSubscriber subscriber)
			{
				this._frameHandler = frameHandler;
				this._subscriber = subscriber;
				this._subscription = subscription;
			}

			void DisposeSubscription()
			{
				Interlocked.Exchange(ref this._subscription, null)?.Dispose();
			}

			public void Dispose()
			{
				if (this._disposed)
					return;

				this._disposed = true;

				if (!this._subscriber.IsCompleted)
					this._frameHandler.OnIncomingCanceled();

				this.DisposeSubscription();
			}

			public void Request(int n)
			{
				if (!this._disposed && !this._subscriber.IsCompleted)
					this._frameHandler.OnRequestN(n);
			}
		}
	}
}
