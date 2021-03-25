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
	public class RequestResponseResponderFrameHandler : RequestStreamResponderFrameHandler
	{
		Task _outputTask;
		public RequestResponseResponderFrameHandler(RSocket socket, int streamId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) : base(socket, streamId, metadata, data, 0, null)
		{

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
			var payload = await this.Socket.Responder((this._data, this._metadata));
			await this.Socket.SendPayload(payload, this.StreamId, next: true, complete: true);
		}
	}
}
