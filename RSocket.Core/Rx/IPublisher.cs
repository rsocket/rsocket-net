using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	public interface IPublisher<T> : IObservable<T>
	{
	}

	public class IncomingStreamWrapper<T> : IObservable<T>
	{
		IObservable<T> _stream;
		RSocket Socket;
		int StreamId;
		public IncomingStreamWrapper(IObservable<T> stream, RSocket socket, int streamId)
		{
			this._stream = stream;
			this.Socket = socket;
			this.StreamId = streamId;
		}

		public IDisposable Subscribe(IObserver<T> observer)
		{
			var sub = this._stream.Subscribe(observer);

			var subscriber = observer as ISubscriber<T>;
			if (subscriber != null)
			{
				Subscription subscription = new Subscription(this.StreamId, this.Socket.Transport.Output);
				subscription.OnCancel += sub.Dispose;  //unsubscribe
				subscriber.OnSubscribe(subscription);
			}

			return sub;
		}
	}
}
