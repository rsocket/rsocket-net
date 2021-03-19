using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RSocket
{
	class IncomingPublisher<T> : IPublisher<T>, IObservable<T>
	{
		FrameHandlerBase _frameHandler;
		IObservable<T> _stream;

		public IncomingPublisher(IObservable<T> stream, FrameHandlerBase frameHandler)
		{
			this._stream = stream;
			this._frameHandler = frameHandler;
		}

		IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
		{
			return (this as IPublisher<T>).Subscribe(observer);
		}

		ISubscription IPublisher<T>.Subscribe(IObserver<T> observer)
		{
			InboundSubscriber<T> inboundSubscriber = new InboundSubscriber<T>(observer, this._frameHandler);
			var sub = this._stream.Subscribe(inboundSubscriber);
			inboundSubscriber.Subscription = sub;
			return inboundSubscriber;
		}
	}
}
