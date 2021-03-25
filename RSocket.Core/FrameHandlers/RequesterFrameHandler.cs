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
		IObservable<Payload> _outgoing;

		public RequesterFrameHandler(RSocket socket
			, int streamId
			, IObservable<Payload> outgoing) : base(socket, streamId)
		{
			this._outgoing = outgoing;
		}

		public override IObservable<Payload> Outgoing { get { return this._outgoing; } }

		protected override void Dispose(bool disposing)
		{

		}
	}
}
