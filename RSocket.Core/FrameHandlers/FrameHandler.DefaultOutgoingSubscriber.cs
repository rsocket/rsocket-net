using System;

namespace RSocket
{
	public abstract partial class FrameHandler
	{
		class DefaultOutgoingSubscriber : Subscriber<Payload>, IObserver<Payload>
		{
			FrameHandler _frameHandler;

			public DefaultOutgoingSubscriber(FrameHandler frameHandler)
			{
				this._frameHandler = frameHandler;
			}

			protected override void DoOnCompleted()
			{
				if (this._frameHandler.OutgoingFinished)
					return;

				if (!this._frameHandler.OutputSingle)
				{
					this._frameHandler.Socket.SendPayload(default(Payload), this._frameHandler.StreamId, true, false).Wait();
				}

				this._frameHandler.FinishOutgoing();
			}

			protected override void DoOnError(Exception error)
			{
				this._frameHandler.OnOutgoingError(error);
			}

			protected override void DoOnNext(Payload value)
			{
				if (this._frameHandler.OutgoingFinished)
					return;

				if (this._frameHandler.OutputSingle)
				{
					this._frameHandler.Socket.SendPayload(value, this._frameHandler.StreamId, true, true).Wait();
					return;
				}

				this._frameHandler.Socket.SendPayload(value, this._frameHandler.StreamId, false, true).Wait();
			}
		}
	}
}
