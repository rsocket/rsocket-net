using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Channeler = System.Func<(System.Buffers.ReadOnlySequence<byte> Data, System.Buffers.ReadOnlySequence<byte> Metadata), System.IObservable<RSocket.Payload>, System.IObservable<RSocket.Payload>>;

namespace RSocket
{
	public class RequestStreamResponderFrameHandler : ResponderFrameHandler
	{
		public RequestStreamResponderFrameHandler(RSocket socket, int streamId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data, int initialRequest, Channeler channeler) : base(socket, streamId, metadata, data, initialRequest, channeler)
		{
			this.Incoming.Subscribe(a => { }, error => { }, () => { });
		}

		public override async Task ToTask()
		{
			this.CancelInput();
			await base.ToTask();
		}
	}
}
