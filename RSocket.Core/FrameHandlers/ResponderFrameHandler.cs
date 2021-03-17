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
		IObservable<int> _requestNObservable;

		public ResponderFrameHandler(RSocket socket, int streamId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data, int initialRequest, Channeler channeler) : base(socket, streamId)
		{
			this._metadata = metadata;
			this._data = data;
			this._channeler = channeler;

			TaskCompletionSource<bool> incomingTaskSignal = new TaskCompletionSource<bool>();

			var inc = Observable.Create<Payload>(observer =>
			{
				this.IncomingReceiver = observer;

				TaskCompletionSource<bool> incomingTaskSignal = new TaskCompletionSource<bool>();
				this._incomingTask = incomingTaskSignal.Task;

				return () =>
				{
					incomingTaskSignal.TrySetResult(true);
				};
			});

			this._incoming = new IncomingPublisher<Payload>(inc, socket, streamId);

			var requestNObservable = Observable.Create<int>(observer =>
			{
				this.RequestNReceiver = observer;
				this.HandleRequestN(new RequestN(this.StreamId, this._data, this._metadata, initialRequest: initialRequest));

				return Disposable.Empty;
			});

			this._requestNObservable = requestNObservable;
		}

		protected override IObservable<Payload> GetOutgoing()
		{
			var outgoing = this._channeler((this._data, this._metadata), this._incoming);     //TODO Handle Errors.
			return outgoing;
		}
		protected override IObservable<int> GetRequestNObservable()
		{
			return this._requestNObservable;
		}

		protected override async Task CreateTask()
		{
			var task = base.CreateTask();
			await Task.WhenAll(task, this._incomingTask);
		}


		protected override void Dispose(bool disposing)
		{
			//this.StopIncoming();
		}
	}
}
