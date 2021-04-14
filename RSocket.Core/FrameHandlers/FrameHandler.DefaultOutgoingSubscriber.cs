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
					this._frameHandler.Socket.SendPayload(this._frameHandler.StreamId, complete: true, next: false).Wait();
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
					this._frameHandler.Socket.SendPayload(this._frameHandler.StreamId, data: value.Data, metadata: value.Metadata, complete: true, next: true).Wait();
					return;
				}

				this._frameHandler.Socket.SendPayload(this._frameHandler.StreamId, data: value.Data, metadata: value.Metadata, complete: false, next: true).Wait();
			}
		}
	}
}
