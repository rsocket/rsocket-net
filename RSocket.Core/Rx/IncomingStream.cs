using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	class IncomingStream : IPublisher<Payload>
	{
		IObservable<Payload> _source;
		FrameHandler _frameHandler;

		int _subscribedFlag = 0;
		bool IsSubscribed
		{
			get
			{
				return Interlocked.CompareExchange(ref this._subscribedFlag, 1, 0) != 0;
			}
		}

		public IncomingStream(IObservable<Payload> source, FrameHandler frameHandler)
		{
			this._source = source;
			this._frameHandler = frameHandler;
		}

		public ISubscription Subscribe(IObserver<Payload> observer)
		{
			if (this.IsSubscribed)
				throw new InvalidOperationException("Incoming stream allows only one Subscriber");

			IncomingStreamSubscriber subscriber = new IncomingStreamSubscriber(observer, this._frameHandler);
			var sub = subscriber.Subscribe(this._source);

			if (this._frameHandler.IncomingFinished)
			{
				subscriber.OnCompleted();
			}

			return new IncomingStreamSubscription(sub, this._frameHandler, subscriber);
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> observer)
		{
			return (this as IPublisher<Payload>).Subscribe(observer);
		}
	}
}
