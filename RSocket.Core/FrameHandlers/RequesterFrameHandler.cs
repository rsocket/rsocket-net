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
			this.OutputCts.Token.Register(this.StopOutging);
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

		void StopIncoming()
		{
			this.IncomingReceiver?.OnCompleted();
		}
		void StopOutging()
		{
			//cancel sending payload.
			this.OutputSubscriber?.OnCompleted();
			this.RequestNReceiver?.OnCompleted();
			this.OutputSubscriberSubscription?.Dispose();
		}

		public override async Task ToTask()
		{
			var outgoing = this._outgoing;

			var outputStream = Observable.Create<Payload>(observer =>
			{
				this.OutputSubscriber = observer;
				this.OutputSubscriberSubscription = outgoing.Subscribe(observer);

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
					this.RequestNReceiver.OnCompleted();
				}, cancel: this.OutputCts.Token);

			await outputTask;
		}

		protected override void Dispose(bool disposing)
		{
			//this.StopIncoming();
		}
	}
}
