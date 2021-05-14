using System;
using System.Buffers;
using System.Threading.Tasks;

namespace RSocket.Channels
{
	public class RequestResponseResponderChannel : RequestStreamResponderChannel, IPublisher<Payload>
	{
		public RequestResponseResponderChannel(RSocket socket, int channelId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) : base(socket, channelId, metadata, data, 1, null)
		{

		}

		protected override IPublisher<Payload> CreateOutgoing()
		{
			return this;
		}

		public override void OnOutgoingNext(Payload payload)
		{
			if (this.OutgoingFinished)
				return;

			this.Socket.SendPayload(this.ChannelId, data: payload.Data, metadata: payload.Metadata, complete: true, next: true);
		}
		public override void OnOutgoingCompleted()
		{
			this.FinishOutgoing();
		}

		public ISubscription Subscribe(IObserver<Payload> subscriber)
		{
			return new Subscription(this, subscriber);
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> subscriber)
		{
			return (this as IPublisher<Payload>).Subscribe(subscriber);
		}

		class Subscription : ISubscription
		{
			ResponderChannel _channel;
			IObserver<Payload> _subscriber;

			public Subscription(ResponderChannel channel, IObserver<Payload> subscriber)
			{
				this._channel = channel;
				this._subscriber = subscriber;
			}

			public void Dispose()
			{

			}

			public void Request(int n)
			{
				var task = this.ExecuteResponderAsync();
			}

			async Task ExecuteResponderAsync()
			{
				await Task.Yield();
				try
				{
					var payload = await this._channel.Socket.Responder((this._channel.Data, this._channel.Metadata));
					this._subscriber.OnNext(payload);
					this._subscriber.OnCompleted();
				}
				catch (Exception ex)
				{
					this._subscriber.OnError(ex);
				}
			}
		}
	}
}
