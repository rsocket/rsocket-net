using RSocket.Channels;
using System;
using System.Threading;

namespace RSocket
{
	class IncomingStream : IPublisher<Payload>
	{
		IPublisher<Payload> _source;
		Channel _channel;

		int _subscribedFlag = 0;
		bool IsSubscribed
		{
			get
			{
				return Interlocked.Exchange(ref this._subscribedFlag, 1) != 0;
			}
		}

		public IncomingStream(IPublisher<Payload> source, Channel channel)
		{
			this._source = source;
			this._channel = channel;
		}

		public ISubscription Subscribe(IObserver<Payload> observer)
		{
			if (this.IsSubscribed)
				throw new InvalidOperationException("Incoming stream allows only one Subscriber");

			IncomingStreamSubscriber subscriber = new IncomingStreamSubscriber(observer, this._channel);
			var sub = this._source.Subscribe(subscriber);

			return new IncomingStreamSubscription(sub, this._channel, subscriber);
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> observer)
		{
			return (this as IPublisher<Payload>).Subscribe(observer);
		}
	}
}
