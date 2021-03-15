using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	class RSocketObserver<T> : IObserver<T>
	{
		IObserver<T> _observer;
		IDisposable _subscription;

		public RSocketObserver(IObserver<T> observer)
		{
			this._observer = observer;
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

				if (this.IsCompleted)
					this._subscription.Dispose();
			}
		}

		public void OnCompleted()
		{
			this.IsCompleted = true;
			this._observer.OnCompleted();
			this.Subscription?.Dispose();
		}

		public void OnError(Exception error)
		{
			this._observer.OnError(error);
		}

		public void OnNext(T value)
		{
			this._observer.OnNext(value);
		}
	}
}
