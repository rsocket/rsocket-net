using RSocket.Channels;
using System;
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
				 * The subscription will be disposed when `this._observer.OnError` or `this._observer.OnCompleted` methods be called in some cases, so we execute `channel.FinishIncoming` method here ensure the incoming status is completed. 
				 */
				this._channel.FinishIncoming();
			}

			this.DisposeSubscription();
			this._channel.OnIncomingSubscriptionCanceled();
		}

		public void Request(int n)
		{
			if (this._disposed)
				return;

			this._channel.OnIncomingSubscriptionRequestN(n);
		}
	}
}
