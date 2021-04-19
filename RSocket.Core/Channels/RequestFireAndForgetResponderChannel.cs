using System;
using System.Buffers;
using System.Threading.Tasks;

namespace RSocket.Channels
{
	public class RequestFireAndForgetResponderChannel : RequestStreamResponderChannel
	{
		public RequestFireAndForgetResponderChannel(RSocket socket, int channelId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) : base(socket, channelId, metadata, data, 0, null)
		{

		}

		public override async Task ToTask()
		{
			try
			{
				await this.Socket.FireAndForgetHandler((this._data, this._metadata));
			}
			catch (Exception ex)
			{
#if DEBUG
				Console.WriteLine($"An exception occurred while handling `RequestFireAndForget` message[{this.ChannelId}: {ex.Message} {ex.StackTrace}");
#endif
			}

			this.FinishOutgoing();
			await base.ToTask();
		}
	}
}
