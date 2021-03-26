using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	class OutgoingStream : IPublisher<Payload>
	{
		IObservable<Payload> _source;

		public OutgoingStream(IObservable<Payload> source)
		{
			this._source = source;
		}

		public ISubscription Subscribe(IObserver<Payload> observer)
		{
			Subscription sub = new Subscription(this._source.Subscribe(observer));
			return sub;
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> observer)
		{
			return (this as IPublisher<Payload>).Subscribe(observer);
		}
	}
}
