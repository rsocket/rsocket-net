using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RSocket
{
	public class Subscriber<T> : IObserver<T>, ISubscription
	{
		ISubscription _subscription;
		bool _completed;
		bool _disposed;
		Exception _error;

		public ISubscription Subscribe(IPublisher<T> provider)
		{
			if (provider == null)
				throw new ArgumentNullException(nameof(provider));

			this._subscription = provider.Subscribe(this);
			return this;
		}
		public ISubscription Subscribe(IObservable<T> provider)
		{
			if (provider == null)
				throw new ArgumentNullException(nameof(provider));

			return this.Subscribe(Helpers.AsPublisher(provider));
		}

		public bool IsCompleted { get { return this._completed; } }
		public Exception Error { get { return this._error; } }

		void DisposeSubscription()
		{
			Interlocked.Exchange(ref this._subscription, null)?.Dispose();
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this._disposed = true;
			this.Finished();
			try
			{
				this.Dispose(true);
			}
			catch
			{
			}
		}

		protected virtual void Dispose(bool disposing)
		{

		}

		void Finished()
		{
			this._completed = true;
			this.DisposeSubscription();
		}

		public void OnCompleted()
		{
			if (this._completed)
				return;

			this.Finished();
			try
			{
				this.DoOnCompleted();
			}
			catch
			{
			}
		}
		protected virtual void DoOnCompleted()
		{
		}

		public void OnError(Exception error)
		{
			if (this._completed)
				return;

			this._error = error;
			this.Finished();
			try
			{
				this.DoOnError(error);
			}
			catch
			{
			}
		}

		protected virtual void DoOnError(Exception error)
		{
		}

		public void OnNext(T value)
		{
			if (this._completed)
				return;

			try
			{
				this.DoOnNext(value);
			}
			catch
			{
				this.Finished();
			}
		}
		protected virtual void DoOnNext(T value)
		{
		}

		public void Request(int n)
		{
			if (this._completed)
				return;

			this.DoRequest(n);
		}
		protected virtual void DoRequest(int n)
		{
			this._subscription?.Request(n);
		}
	}
}
