using System;
using System.Threading;

namespace RSocket
{
	public class Publisher<T> : IPublisher<T>, IObservable<T>, IObserver<T>, ISubscription, IDisposable
	{
		IObserver<T> _subscriber;
		int _stoppedFlag;
		int _disposedFlag;

		public bool IsDisposed { get { return Volatile.Read(ref this._disposedFlag) != 0; } }
		public bool IsStopped { get { return Volatile.Read(ref this._stoppedFlag) != 0; } }

		public Publisher()
		{
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref this._disposedFlag, 1) != 0)
				return;

			this.Dispose(true);
		}
		protected virtual void Dispose(bool disposing)
		{
		}

		public void OnCompleted()
		{
			if (Interlocked.Exchange(ref this._stoppedFlag, 1) != 0 || this.IsDisposed)
				return;

			try
			{
				this._subscriber?.OnCompleted();
			}
			finally
			{
				this.Dispose();
			}
		}

		public void OnError(Exception error)
		{
			if (Interlocked.Exchange(ref this._stoppedFlag, 1) != 0 || this.IsDisposed)
				return;

			try
			{
				this._subscriber?.OnError(error);
			}
			finally
			{
				this.Dispose();
			}
		}

		public void OnNext(T value)
		{
			if (this.IsStopped || this.IsDisposed)
				return;

			try
			{
				this._subscriber?.OnNext(value);
			}
			catch
			{
				Interlocked.Exchange(ref this._stoppedFlag, 1);
				this.Dispose();
				throw;
			}
		}

		public virtual ISubscription Subscribe(IObserver<T> observer)
		{
			if (observer == null)
				throw new ArgumentNullException(nameof(observer));

			if (this.IsDisposed)
			{
				throw new ObjectDisposedException(this.GetType().FullName);
			}

			Interlocked.CompareExchange(ref this._subscriber, observer, null);
			return this;
		}

		IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
		{
			return (this as IPublisher<T>).Subscribe(observer);
		}

		public void Request(int n)
		{
			if (this.IsStopped || this.IsDisposed)
				return;

			this.RequestCore(n);
		}
		protected virtual void RequestCore(int n)
		{

		}
	}
}
