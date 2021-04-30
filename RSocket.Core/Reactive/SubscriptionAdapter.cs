using System;
using System.Threading;

namespace RSocket
{
	public class SubscriptionAdapter : ISubscription
	{
		IDisposable _subscription;
		bool _disposed;

		public SubscriptionAdapter(IDisposable subscription)
		{
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
			this.DisposeSubscription();
		}

		public void Request(int n)
		{
			if (this._disposed)
				return;

			ISubscription sub = this._subscription as ISubscription;
			sub?.Request(n);
		}
	}
}
