using RSocket.Channels;
using System;
using System.Threading.Tasks;

namespace RSocket
{
	class RequestFireAndForgetRequesterIncomingStream : RequestStreamRequesterIncomingStream
	{
		public RequestFireAndForgetRequesterIncomingStream(RSocket socket, Func<int, Task> channelEstablisher) : base(socket, channelEstablisher)
		{

		}

		protected override void OnSubscribe(Channel channel)
		{
			base.OnSubscribe(channel);
			channel.FinishIncoming();
		}
	}
}
