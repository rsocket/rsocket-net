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
			OutgoingStreamSubscription sub = new OutgoingStreamSubscription(this._source.Subscribe(observer));
			return sub;
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> observer)
		{
			return (this as IPublisher<Payload>).Subscribe(observer);
		}

		class OutgoingStreamSubscription : ISubscription
		{
			IDisposable _rxSubscription;
			bool _disposed;

			public OutgoingStreamSubscription(IDisposable rxSubscription)
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
}
