using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RSocket
{
	class IncomingStreamSubscription : ISubscription
	{
		IDisposable _subscription;
		Channel _channel;
		IncomingStreamSubscriber _subscriber;

		bool _disposed;

		public IncomingStreamSubscription(IDisposable subscription, Channel channel, IncomingStreamSubscriber subscriber)
		{
			this._subscription = subscription;
			this._channel = channel;
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

			this._disposed = true;

			if (this._subscriber.IsCompleted)
			{
				/* 
				 * The subscription will be disposed when `this._observer.OnError` or `this._observer.OnCompleted` methods calling in some cases, so we execute `OnIncomingCompleted` method again ensure the incoming status is completed. 
				 */
				this._channel.OnIncomingCompleted();
			}

			this.DisposeSubscription();

			this._channel.OnIncomingCanceled();
		}

		public void Request(int n)
		{
			if (this._disposed)
				return;

			if (this._subscriber.IsCompleted)
				return;

			this._channel.OnIncomingSubscriberRequestN(n);
		}
	}
}
