using RSocket.Channels;
using System;
using System.Threading.Tasks;

namespace RSocket
{
	class RequestStreamRequesterIncomingStream : RequesterIncomingStream
	{
		public RequestStreamRequesterIncomingStream(RSocket socket, Func<int, Task> channelEstablisher) : base(socket, new SimplePublisher<Payload>(), channelEstablisher)
		{

		}

		protected override void OnSubscribe(Channel channel)
		{
			base.OnSubscribe(channel);
			channel.FinishOutgoing();
		}
	}
}
