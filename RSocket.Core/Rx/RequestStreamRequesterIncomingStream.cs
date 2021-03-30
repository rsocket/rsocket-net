using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RSocket
{
	class RequestStreamRequesterIncomingStream : RequesterIncomingStream
	{
		public RequestStreamRequesterIncomingStream(RSocket socket, Func<int, Task> channelEstablisher) : base(socket, new SimplePublisher<Payload>(), channelEstablisher)
		{

		}

		protected override void OnSubscribe(int streamId, FrameHandler frameHandler)
		{
			base.OnSubscribe(streamId, frameHandler);
			frameHandler.FinishOutgoing();
		}
	}
}
