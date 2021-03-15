using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
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
