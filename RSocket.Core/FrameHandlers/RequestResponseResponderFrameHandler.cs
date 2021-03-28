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

namespace RSocket
{
	public class RequestResponseResponderFrameHandler : RequestStreamResponderFrameHandler
	{
		public RequestResponseResponderFrameHandler(RSocket socket, int streamId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) : base(socket, streamId, metadata, data, 0, null)
		{

		}

		protected override bool OutputSingle => true;

		public override async Task ToTask()
		{
			try
			{
				var payload = await this.Socket.Responder((this._data, this._metadata));
				this.OutgoingSubscriber.OnNext(payload);
				this.OutgoingSubscriber.OnCompleted();
			}
			catch (Exception ex)
			{
				this.OutgoingSubscriber.OnError(ex);
			}

			await base.ToTask();
		}
	}
}
