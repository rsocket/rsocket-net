using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	class IncomingStreamSubscriber : Subscriber<Payload>, IObserver<Payload>, ISubscription
	{
		IObserver<Payload> _observer;
		FrameHandler _frameHandler;

		public IncomingStreamSubscriber(IObserver<Payload> observer, FrameHandler frameHandler)
		{
			this._observer = observer;
			this._frameHandler = frameHandler;
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

			this._frameHandler.OnIncomingCompleted();
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

			this._frameHandler.OnIncomingCompleted();
		}

		protected override void DoOnNext(Payload value)
		{
			try
			{
				this._observer.OnNext(value);
			}
			catch
			{
				this._frameHandler.OnIncomingCanceled();
				throw;
			}
		}
	}
}
