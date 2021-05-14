using System;
using System.Threading.Tasks;

namespace RSocket
{
	class RequestResponseRequesterIncomingStream : RequestStreamRequesterIncomingStream
	{
		public RequestResponseRequesterIncomingStream(RSocket socket, Func<int, Task> channelEstablisher) : base(socket, channelEstablisher)
		{

		}
	}
}
