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
		TaskCompletionSource<bool> _waitCompleteHandler;

		public IncomingStream(IObservable<Payload> source, FrameHandler frameHandler, TaskCompletionSource<bool> waitCompleteHandler)
		{
			this._source = source;
			this._frameHandler = frameHandler;
			this._waitCompleteHandler = waitCompleteHandler;
		}

		public ISubscription Subscribe(IObserver<Payload> observer)
		{
			IncomingStreamSubscriber subscriber = new IncomingStreamSubscriber(observer, this._waitCompleteHandler);
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
			TaskCompletionSource<bool> _waitCompleteHandler;

			bool _completed;
			Exception _error;

			public IncomingStreamSubscriber(IObserver<Payload> observer, TaskCompletionSource<bool> waitCompleteHandler)
			{
				this._observer = observer;
				this._waitCompleteHandler = waitCompleteHandler;
			}

			public bool Completed { get { return this._completed; } }
			public TaskCompletionSource<bool> WaitCompleteHandler { get { return this._waitCompleteHandler; } }

			public void OnCompleted()
			{
				if (this._completed)
					return;

				this._completed = true;
				this._observer.OnCompleted();
				this._waitCompleteHandler.TrySetResult(true);
			}

			public void OnError(Exception error)
			{
				if (this._completed)
					return;

				this._completed = true;
				this._error = error;
				this._observer.OnError(error);

				try
				{
					this._waitCompleteHandler.TrySetResult(true);
				}
				catch
				{
				}
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

				if (!this._subscriber.Completed)
					this._frameHandler.SendCancel();

				this._subscriber.WaitCompleteHandler.TrySetResult(true);

				this._disposed = true;
			}

			public void Request(int n)
			{
				if (!this._disposed && !this._subscriber.Completed)
					this._frameHandler.SendRequest(n);
			}
		}
	}

}
