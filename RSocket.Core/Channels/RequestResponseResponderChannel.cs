using System;
using System.Buffers;
using System.Threading.Tasks;

namespace RSocket
{
	public class RequestResponseResponderChannel : RequestStreamResponderChannel
	{
		public RequestResponseResponderChannel(RSocket socket, int channelId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) : base(socket, channelId, metadata, data, 0, null)
		{

		}

		protected override bool OutputSingle => true;

		public override async Task ToTask()
		{
			try
			{
				var payload = await this.Socket.Responder((this._data, this._metadata));
				this.OutgoingSubscriber.OnNext(payload);
				this.OutgoingSubscriber.OnCompleted();
			}
			catch (Exception ex)
			{
				this.OutgoingSubscriber.OnError(ex);
			}

			await base.ToTask();
		}
	}
}
