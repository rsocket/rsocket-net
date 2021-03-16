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
		public RequestStreamRequesterIncomingStream(RSocket socket, Func<int, Task> channelEstablisher) : base(socket, GetOutputs(), channelEstablisher)
		{

		}

		static IObservable<Payload> GetOutputs()
		{
			var outputs = Observable.Create<Payload>(observer =>
			  {
				  return Disposable.Empty;
			  });

			return outputs;
		}

		protected override void OnSubscribe(int streamId, IFrameHandler frameHandler)
		{
			base.OnSubscribe(streamId, frameHandler);

			frameHandler.HandleCancel(new RSocketProtocol.Cancel(streamId));
		}
	}
}
