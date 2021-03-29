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
			var sub = this._source.Subscribe(subscriber);

			return new IncomingStreamSubscription(sub, this._frameHandler, subscriber);
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> observer)
		{
			return (this as IPublisher<Payload>).Subscribe(observer);
		}

		class IncomingStreamSubscriber : IObserver<Payload>
		{
			IObserver<Payload> _observer;
			FrameHandler _frameHandler;

			bool _completed;
			Exception _error;

			public IncomingStreamSubscriber(IObserver<Payload> observer, FrameHandler frameHandler)
			{
				this._observer = observer;
				this._frameHandler = frameHandler;
			}

			public bool Completed { get { return this._completed; } }

			public void OnCompleted()
			{
				if (this._completed)
					return;

				this._completed = true;
				this._observer.OnCompleted();
				this._frameHandler.OnIncomingCompleted();
			}

			public void OnError(Exception error)
			{
				if (this._completed)
					return;

				this._completed = true;
				this._error = error;
				this._observer.OnError(error);
				this._frameHandler.OnIncomingCompleted();
			}

			public void OnNext(Payload value)
			{
				if (this._completed)
					return;

				this._observer.OnNext(value);
			}
		}

		class IncomingStreamSubscription : ISubscription
		{
			IDisposable _subscription;
			FrameHandler _frameHandler;
			IncomingStreamSubscriber _subscriber;

			bool _disposed = false;

			public IncomingStreamSubscription(IDisposable subscription, FrameHandler frameHandler, IncomingStreamSubscriber subscriber)
			{
				this._subscription = subscription;
				this._frameHandler = frameHandler;
				this._subscriber = subscriber;
			}

			void DisposeSubscription()
			{
				Interlocked.Exchange(ref this._subscription, null)?.Dispose();
			}

			public void Dispose()
			{
				if (this._disposed)
					return;

				this.DisposeSubscription();

				this._frameHandler.OnIncomingCanceled();
				this._disposed = true;
			}

			public void Request(int n)
			{
				if (!this._disposed && !this._subscriber.Completed)
					this._frameHandler.OnRequestN(n);
			}
		}
	}

}
