using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	class OutboundSubscription : ISubscription
	{
		IDisposable _rxSubscription;
		bool _disposed;

		public OutboundSubscription(IDisposable rxSubscription)
		{
			this._rxSubscription = rxSubscription;
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this._rxSubscription.Dispose();
			this._disposed = true;
		}

		public void Request(int n)
		{
			if (this._disposed)
				return;

			ISubscription sub = this._rxSubscription as ISubscription;
			sub?.Request(n);
		}
	}
}
