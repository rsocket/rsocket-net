using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Channeler = System.Func<(System.Buffers.ReadOnlySequence<byte> Data, System.Buffers.ReadOnlySequence<byte> Metadata), System.IObservable<RSocket.PayloadContent>, System.IObservable<RSocket.PayloadContent>>;

namespace RSocket
{
	public class ResponderFrameHandler : FrameHandlerBase
	{
		ReadOnlySequence<byte> _metadata;
		ReadOnlySequence<byte> _data;
		Channeler _channeler;

		IncomingStreamWrapper<PayloadContent> _incoming;
		Task _incomingTask;
		IObservable<int> _requestNObservable;
		IObserver<PayloadContent> _incomingReceiver;
		IObserver<int> _requestNReceiver;

		IObserver<PayloadContent> _outputSubscriber;
		IDisposable _outputSubscriberSubscription;

		public ResponderFrameHandler(RSocket socket, int streamId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data, int initialRequest, Channeler channeler) : base(socket)
		{
			this.StreamId = streamId;
			this._metadata = metadata;
			this._data = data;
			this._channeler = channeler;

			TaskCompletionSource<bool> incomingTaskSignal = new TaskCompletionSource<bool>();

			var inc = Observable.Create<PayloadContent>(observer =>
			{
				this._incomingReceiver = observer;

				TaskCompletionSource<bool> incomingTaskSignal = new TaskCompletionSource<bool>();
				this._incomingTask = incomingTaskSignal.Task;

				return () =>
				{
					incomingTaskSignal.TrySetResult(true);
				};
			});

			this._incoming = new IncomingStreamWrapper<PayloadContent>(inc, socket, streamId);

			var requestNObservable = Observable.Create<int>(observer =>
			{
				this._requestNReceiver = observer;
				this._requestNReceiver.OnNext(initialRequest);
				this.NotifyOutputPublisher(initialRequest);
				return Disposable.Empty;
			});

			this._requestNObservable = requestNObservable;

			this.OutputCts.Token.Register(this.StopOutging);
		}

		public int StreamId { get; set; }

		void NotifyOutputPublisher(int n)
		{
			ISubscription sub = this._outputSubscriberSubscription as ISubscription;
			sub?.Request(n);
		}

		protected override IObserver<PayloadContent> GetPayloadHandler()
		{
			return this._incomingReceiver;
		}

		public override void HandleRequestN(RSocketProtocol.RequestN message)
		{
			var handler = this.GetRequestNHandler();

#if DEBUG
			if (handler == null)
				Console.WriteLine("missing reuqest(n) handler");
#endif

			if (handler != null)
			{
				handler.OnNext(message.RequestNumber);
				this.NotifyOutputPublisher(message.RequestNumber);
			}
		}
		protected override IObserver<int> GetRequestNHandler()
		{
			return this._requestNReceiver;
		}

		void StopIncoming()
		{
			this._incomingReceiver?.OnCompleted();
		}
		void StopOutging()
		{
			//cancel sending payload.
			this._outputSubscriber?.OnCompleted();
			this._requestNReceiver?.OnCompleted();
			this._outputSubscriberSubscription?.Dispose();
		}

		public override async Task AsTask()
		{
			var outgoing = this._channeler((this._data, this._metadata), this._incoming);     //TODO Handle Errors.

			var outputStream = Observable.Create<PayloadContent>(observer =>
			{
				this._outputSubscriber = observer;
				this._outputSubscriberSubscription = outgoing.Subscribe(observer);

				return Disposable.Empty;
			});

			var outputPayloads = Helpers.MakeControllableStream(outputStream, this._requestNObservable);

			var outputTask = Helpers.ForEach(outputPayloads,
				action: async value =>
				{
					await new RSocketProtocol.Payload(this.StreamId, value.Data, value.Metadata, next: true).WriteFlush(this.Socket.Transport.Output, value.Data, value.Metadata);
				},
				final: async () =>
				{
					await new RSocketProtocol.Payload(this.StreamId, complete: true).WriteFlush(this.Socket.Transport.Output);
					this._requestNReceiver.OnCompleted();
				}, cancel: this.OutputCts.Token);

			this.OnPrepare();
			await Task.WhenAll(outputTask, this._incomingTask);
		}

		protected virtual void OnPrepare()
		{

		}

		protected override void Dispose(bool disposing)
		{
			//this.StopIncoming();
		}
	}
}
