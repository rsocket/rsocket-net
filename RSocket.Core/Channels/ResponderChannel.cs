using System;
using System.Buffers;
using Channeler = System.Func<(System.Buffers.ReadOnlySequence<byte> Data, System.Buffers.ReadOnlySequence<byte> Metadata), RSocket.IPublisher<RSocket.Payload>, System.IObservable<RSocket.Payload>>;

namespace RSocket
{
	public class ResponderChannel : Channel
	{
		protected ReadOnlySequence<byte> _metadata;
		protected ReadOnlySequence<byte> _data;
		Channeler _channeler;

		public ResponderChannel(RSocket socket, int channelId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data, int initialRequest, Channeler channeler) : base(socket, channelId, initialRequest)
		{
			this._metadata = metadata;
			this._data = data;
			this._channeler = channeler;
		}

		protected override IPublisher<Payload> CreateOutgoing()
		{
			if (this._channeler == null)
			{
				return base.CreateOutgoing();
			}

			var outputPayloads = this._channeler((this._data, this._metadata), this.Incoming);
			return Helpers.AsPublisher(outputPayloads);
		}

		protected override void Dispose(bool disposing)
		{

		}

		protected override void HandleCancelCore()
		{
			base.HandleCancelCore();
			this.IncomingSubscriber?.OnError(new OperationCanceledException("Inbound has been canceled."));
		}
	}
}
