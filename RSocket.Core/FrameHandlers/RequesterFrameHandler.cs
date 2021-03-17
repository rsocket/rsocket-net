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
		IObservable<int> _requestNObservable;

		public RequesterFrameHandler(RSocket socket
			, int streamId
			, IObserver<Payload> incomingReceiver
			, IObservable<Payload> outgoing) : base(socket, streamId)
		{
			this._outgoing = outgoing;

			this.IncomingReceiver = incomingReceiver;

			var requestNObservable = Observable.Create<int>(observer =>
			{
				this.RequestNReceiver = observer;
				return Disposable.Empty;
			});

			this._requestNObservable = requestNObservable;
		}

		protected override IObservable<Payload> GetOutgoing()
		{
			var outgoing = _outgoing;     //TODO Handle Errors.
			return outgoing;
		}
		protected override IObservable<int> GetRequestNObservable()
		{
			return this._requestNObservable;
		}

		protected override void Dispose(bool disposing)
		{
			//this.StopIncoming();
		}
	}
}
