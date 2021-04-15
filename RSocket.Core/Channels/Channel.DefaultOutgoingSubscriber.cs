using System;

namespace RSocket
{
	public abstract partial class Channel
	{
		class DefaultOutgoingSubscriber : Subscriber<Payload>, IObserver<Payload>
		{
			Channel _channel;

			public DefaultOutgoingSubscriber(Channel channel)
			{
				this._channel = channel;
			}

			protected override void DoOnCompleted()
			{
				if (this._channel.OutgoingFinished)
					return;

				if (!this._channel.OutputSingle)
				{
					this._channel.Socket.SendPayload(this._channel.ChannelId, complete: true, next: false).Wait();
				}

				this._channel.FinishOutgoing();
			}

			protected override void DoOnError(Exception error)
			{
				this._channel.OnOutgoingError(error);
			}

			protected override void DoOnNext(Payload value)
			{
				if (this._channel.OutgoingFinished)
					return;

				if (this._channel.OutputSingle)
				{
					this._channel.Socket.SendPayload(this._channel.ChannelId, data: value.Data, metadata: value.Metadata, complete: true, next: true).Wait();
					return;
				}

				this._channel.Socket.SendPayload(this._channel.ChannelId, data: value.Data, metadata: value.Metadata, complete: false, next: true).Wait();
			}
		}
	}
}
