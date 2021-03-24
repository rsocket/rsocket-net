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
	public class RequesterFrameHandler : FrameHandlerBase
	{
		IObservable<Payload> _outgoing;
		TaskCompletionSource<bool> _inboundTaskSignal = new TaskCompletionSource<bool>();

		public RequesterFrameHandler(RSocket socket
			, int streamId
			, IObserver<Payload> inboundSubscriber
			, IObservable<Payload> outgoing) : base(socket, streamId)
		{
			this._outgoing = outgoing;
			this.InboundSubscriber = inboundSubscriber;
		}

		public TaskCompletionSource<bool> InboundTaskSignal { get { return this._inboundTaskSignal; } }

		protected override void StopIncoming()
		{
			this._inboundTaskSignal.TrySetResult(true);
		}

		protected override Task GetInputTask()
		{
			return this._inboundTaskSignal.Task;
		}

		protected override IObservable<Payload> GetOutgoing()
		{
			var outgoing = _outgoing;
			return outgoing;
		}

		protected override void Dispose(bool disposing)
		{

		}
	}
}
