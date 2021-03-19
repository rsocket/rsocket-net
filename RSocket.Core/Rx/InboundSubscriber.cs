using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RSocket
{
	class InboundSubscriber<T> : IObserver<T>, ISubscription
	{
		IObserver<T> _subscriber;
		FrameHandlerBase _frameHandler;
		IDisposable _subscription;

		Exception _error;
		bool _disposed = false;

		public InboundSubscriber(IObserver<T> subscriber, FrameHandlerBase frameHandler)
		{
			this._subscriber = subscriber;
			this._frameHandler = frameHandler;
		}

		public bool IsCompleted { get; set; } = false;
		public IDisposable Subscription
		{
			get
			{
				return this._subscription;
			}
			set
			{
				this._subscription = value;

				if (this.HasFinished())
					this.DisposeSubscription();
			}
		}

		bool HasFinished()
		{
			return this.IsCompleted || this._error != null;
		}

		public void OnCompleted()
		{
			if (this.HasFinished())
				return;

			this.IsCompleted = true;
			this._subscriber?.OnCompleted();
			this.DisposeSubscription();
		}

		public void OnError(Exception error)
		{
			if (this.HasFinished())
				return;

			this._error = error;
			this._subscriber.OnError(error);
			this.DisposeSubscription();
		}

		public void OnNext(T value)
		{
			if (this.HasFinished())
				return;

			this._subscriber.OnNext(value);
		}

		void DisposeSubscription()
		{
			Interlocked.Exchange(ref this._subscription, null)?.Dispose();
		}

		public void Request(int n)
		{
			if (this.HasFinished())
				return;

			this._frameHandler.SendRequest(n);
		}

		void Cancel()
		{
			if (this.HasFinished())
				return;

			this._frameHandler.SendCancel();
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this.Cancel();
			this.DisposeSubscription();
			this.Dispose(true);

			this._disposed = true;
		}

		protected virtual void Dispose(bool dispoing)
		{

		}
	}
}
