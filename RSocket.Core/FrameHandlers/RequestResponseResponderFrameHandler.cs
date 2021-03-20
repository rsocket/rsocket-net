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
	public class RequestResponseResponderFrameHandler : RequestStreamResponderFrameHandler
	{
		Task _outputTask;
		public RequestResponseResponderFrameHandler(RSocket socket, int streamId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) : base(socket, streamId, metadata, data, 0, null)
		{

		}

		protected override void OnTaskCreated()
		{
			var payloadHandler = this.GetPayloadHandler();
			payloadHandler?.OnCompleted();
		}

		protected override void OnTaskCreating()
		{
			this._outputTask = this.CreateTask();
		}
		protected override Task GetOutputTask()
		{
			return this._outputTask;
		}
		async Task CreateTask()
		{
			var value = await this.Socket.Responder((this._data, this._metadata));     //TODO Handle Errors.
			await new RSocketProtocol.Payload(this.StreamId, value.Data, value.Metadata, next: true, complete: true).WriteFlush(this.Socket.Transport.Output, value.Data, value.Metadata);
		}
	}
}
