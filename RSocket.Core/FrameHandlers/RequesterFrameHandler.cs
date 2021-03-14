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
		IObservable<PayloadContent> _outgoing;

		IObservable<int> _requestNObservable;
		IObserver<PayloadContent> _incomingReceiver;
		IObserver<int> _requestNReceiver;

		IObserver<PayloadContent> _outputSubscriber;
		IDisposable _outputSubscriberSubscription;

		public RequesterFrameHandler(RSocket socket
			, int streamId
			, IObserver<PayloadContent> incomingReceiver
			, IObservable<PayloadContent> outgoing) : base(socket)
		{
			this.StreamId = streamId;
			this._outgoing = outgoing;

			this._incomingReceiver = incomingReceiver;

			var requestNObservable = Observable.Create<int>(observer =>
			{
				this._requestNReceiver = observer;
				return Disposable.Empty;
			});

			this._requestNObservable = requestNObservable;
			this.OutputCts.Token.Register(this.StopOutging);
		}

		public int StreamId { get; set; }

		protected override IObserver<PayloadContent> GetPayloadHandler()
		{
			return this._incomingReceiver;
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
			var outgoing = this._outgoing;

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

			await outputTask;
		}

		protected override void Dispose(bool disposing)
		{
			//this.StopIncoming();
		}
	}
}
