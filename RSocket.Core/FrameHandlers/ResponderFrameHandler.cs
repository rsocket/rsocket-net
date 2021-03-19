using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static RSocket.RSocketProtocol;
using Channeler = System.Func<(System.Buffers.ReadOnlySequence<byte> Data, System.Buffers.ReadOnlySequence<byte> Metadata), RSocket.IPublisher<RSocket.Payload>, System.IObservable<RSocket.Payload>>;

namespace RSocket
{
	public class ResponderFrameHandler : FrameHandlerBase
	{
		ReadOnlySequence<byte> _metadata;
		ReadOnlySequence<byte> _data;
		Channeler _channeler;

		IncomingPublisher<Payload> _incoming;
		Task _incomingTask;
		TaskCompletionSource<bool> _incomingTaskSignal;

		public ResponderFrameHandler(RSocket socket, int streamId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data, int initialRequest, Channeler channeler) : base(socket, streamId, initialRequest)
		{
			this._metadata = metadata;
			this._data = data;
			this._channeler = channeler;
		}

		protected override IObservable<Payload> GetOutgoing()
		{
			var outgoing = this._channeler((this._data, this._metadata), this._incoming);     //TODO Handle Errors.
			return outgoing;
		}
		protected override Task GetInputTask()
		{
			return this._incomingTask;
		}

		protected override void OnTaskCreating()
		{
			var inc = Observable.Create<Payload>(observer =>
			{
				this.InboundSubscriber = observer;

				TaskCompletionSource<bool> incomingTaskSignal = new TaskCompletionSource<bool>();
				this._incomingTaskSignal = incomingTaskSignal;
				this._incomingTask = incomingTaskSignal.Task;

				return () =>
				{
					incomingTaskSignal.TrySetResult(true);
				};
			});

			this._incoming = new IncomingPublisher<Payload>(inc, this);

			base.OnTaskCreating();
		}

		protected override void StopIncoming()
		{
			this._incomingTaskSignal?.TrySetResult(true);
		}

		protected override void Dispose(bool disposing)
		{
			//this.StopIncoming();
		}
	}
}
