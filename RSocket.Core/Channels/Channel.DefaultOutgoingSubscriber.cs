using System;

namespace RSocket.Channels
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
				this._channel.OnOutgoingCompleted();
			}

			protected override void DoOnError(Exception error)
			{
				this._channel.OnOutgoingError(error);
			}

			protected override void DoOnNext(Payload payload)
			{
				this._channel.OnOutgoingNext(payload);
			}
		}
	}
}
