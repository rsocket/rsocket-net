using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Channeler = System.Func<(System.Buffers.ReadOnlySequence<byte> Data, System.Buffers.ReadOnlySequence<byte> Metadata), RSocket.IPublisher<RSocket.Payload>, System.IObservable<RSocket.Payload>>;

namespace RSocket
{
	public class ResponderFrameHandler : FrameHandler
	{
		protected ReadOnlySequence<byte> _metadata;
		protected ReadOnlySequence<byte> _data;
		Channeler _channeler;

		public ResponderFrameHandler(RSocket socket, int streamId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data, int initialRequest, Channeler channeler) : base(socket, streamId, initialRequest)
		{
			this._metadata = metadata;
			this._data = data;
			this._channeler = channeler;
		}

		protected override IPublisher<Payload> CreateOutging()
		{
			if (this._channeler == null)
			{
				return base.CreateOutging();
			}

			var outputPayloads = this._channeler((this._data, this._metadata), this.Incoming);
			return Helpers.AsPublisher(outputPayloads);
		}

		protected override void Dispose(bool disposing)
		{

		}

		public override void HandleCancel(RSocketProtocol.Cancel message)
		{
			base.HandleCancel(message);
			this.IncomingSubscriber?.OnError(new OperationCanceledException("Inbound has been canceled."));
			this.CancelInput();
		}
	}
}
