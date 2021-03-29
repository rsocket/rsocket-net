using System;
using System.Threading;

namespace RSocket
{
	public interface ISubscription : IDisposable
	{
		void Request(int n);
	}

	public class Subscription : ISubscription
	{
		IDisposable _rxSubscription;
		bool _disposed;

		public Subscription(IDisposable rxSubscription)
		{
			this._rxSubscription = rxSubscription;
		}

		void DisposeSubscription()
		{
			Interlocked.Exchange(ref this._rxSubscription, null)?.Dispose();
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this._disposed = true;
			this.DisposeSubscription();
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

		public void Request(int n)
		{
			if (this._disposed)
				return;

			this.DoRequest(n);
		}

		protected virtual void DoRequest(int n)
		{
			ISubscription sub = this._rxSubscription as ISubscription;
			sub?.Request(n);
		}
	}
}
