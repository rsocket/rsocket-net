using System;

namespace RSocket
{
	public interface ISubscription : IDisposable
	{
		void Request(int n);
	}

	class Subscription : ISubscription
	{
		IDisposable _rxSubscription;
		bool _disposed;

		public Subscription(IDisposable rxSubscription)
		{
			this._rxSubscription = rxSubscription;
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this._rxSubscription.Dispose();
			this.Dispose(true);
			this._disposed = true;
		}

		protected virtual void Dispose(bool disposing)
		{

		}

		public virtual void Request(int n)
		{
			if (this._disposed)
				return;

			ISubscription sub = this._rxSubscription as ISubscription;
			sub?.Request(n);
		}
	}
}
