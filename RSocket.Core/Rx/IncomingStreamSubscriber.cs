using RSocket.Channels;
using System;

namespace RSocket
{
	class IncomingStreamSubscriber : Subscriber<Payload>, IObserver<Payload>, ISubscription
	{
		IObserver<Payload> _observer;
		Channel _channel;

		public IncomingStreamSubscriber(IObserver<Payload> observer, Channel channel)
		{
			this._observer = observer;
			this._channel = channel;
		}

		protected override void DoOnCompleted()
		{
			try
			{
				this._observer.OnCompleted();
			}
			catch
			{
			}

			this._channel.OnIncomingCompleted();
		}

		protected override void DoOnError(Exception error)
		{
			try
			{
				this._observer.OnError(error);
			}
			catch
			{
			}

			this._channel.OnIncomingCompleted();
		}

		protected override void DoOnNext(Payload value)
		{
			try
			{
				this._observer.OnNext(value);
			}
			catch
			{
				this._channel.OnIncomingCanceled();
				throw;
			}
		}
	}
}
