using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public class RequesterFrameHandler : FrameHandler
	{
		IPublisher<Payload> _outgoing;

		public RequesterFrameHandler(RSocket socket
			, int streamId
			, IObservable<Payload> outgoing) : base(socket, streamId)
		{
			this._outgoing = Helpers.AsPublisher(outgoing);
		}

		protected override IPublisher<Payload> CreateOutging()
		{
			return this._outgoing;
		}

		public override void OnIncomingCanceled()
		{
			if (this.IncomingFinished && this.OutgoingFinished)
				return;

			this.FinishIncoming();
			this.FinishOutgoing();
			this.SendCancelFrame();
		}
	}
}
