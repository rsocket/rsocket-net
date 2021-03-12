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
using IRSocketStream = System.IObserver<RSocket.PayloadContent>;

namespace RSocket
{
	public interface IFrameHandler : IDisposable
	{
		void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void HandleRequestN(RSocketProtocol.RequestN message);
		void HandleCancel(RSocketProtocol.Cancel message);
		Task AsTask();
	}

	public class FrameHandlerBase : IFrameHandler
	{
		public FrameHandlerBase(RSocket socket)
		{
			this.Socket = socket;
		}

		public RSocket Socket { get; set; }
		public CancellationTokenSource CancelTrigger { get; set; } = new CancellationTokenSource();

		public virtual void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			var incoming = this.GetPayloadHandler();
			if (incoming != null)
			{
				if (message.IsNext)
				{
					incoming.OnNext(new PayloadContent(data, metadata));
				}

				if (message.IsComplete)
				{
					incoming.OnCompleted();
				}
			}
		}
		protected virtual IObserver<PayloadContent> GetPayloadHandler()
		{
			return null;
		}

		public virtual void HandleRequestN(RSocketProtocol.RequestN message)
		{
			var handler = this.GetRequestNHandler();
			if (handler != null)
			{
				handler.OnNext(message.RequestNumber);
			}
		}

		protected virtual IObserver<int> GetRequestNHandler()
		{
			return null;
		}

		public virtual void HandleCancel(RSocketProtocol.Cancel message)
		{
			this.CancelTrigger.Cancel();
			Console.WriteLine($"this.CancelTrigger.Cancel() {this.CancelTrigger.Token.IsCancellationRequested}");
		}

		public virtual Task AsTask()
		{
			return Task.CompletedTask;
		}

		public void Dispose()
		{
			if (!this.CancelTrigger.IsCancellationRequested)
				this.CancelTrigger.Cancel();

			this.CancelTrigger.Dispose();

			this.Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{

		}
	}

	public class RequestStreamRequesterFrameHandler : FrameHandlerBase
	{
		ReadOnlySequence<byte> _metadata;
		ReadOnlySequence<byte> _data;

		IObserver<PayloadContent> _incomingPayloadReceiver;

		IObservable<PayloadContent> _incomingPayloadPublisher;

		public RequestStreamRequesterFrameHandler(RSocket socket
			, int streamId
			, ISubscriber<PayloadContent> subscriber
			, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data
			, int initial) : base(socket)
		{
			this._metadata = metadata;
			this._data = data;
			this.StreamId = streamId;

			var incomingPayloadPublisher = Observable.Create<PayloadContent>(observer =>
			{
				StreamReceiver receiver = new StreamReceiver(subscriber, observer);
				Subscription subscription = new Subscription(streamId, this.Socket.Transport.Output);
				subscription.OnCancel += () =>
				{
					this.CancelTrigger.Cancel();
					receiver.OnCompleted();
				};
				subscriber.OnSubscribe(subscription);
				new RSocketProtocol.RequestStream(streamId, data, metadata, initialRequest: this.Socket.Options.GetInitialRequestSize(initial)).WriteFlush(this.Socket.Transport.Output, data, metadata).ConfigureAwait(false);

				this._incomingPayloadReceiver = receiver;

				return Disposable.Empty;
			});

			this._incomingPayloadPublisher = incomingPayloadPublisher;
		}

		public int StreamId { get; set; }

		protected override IObserver<PayloadContent> GetPayloadHandler()
		{
			return this._incomingPayloadReceiver;
		}

		public override async Task AsTask()
		{
			await this._incomingPayloadPublisher.ToAsyncEnumerable().LastOrDefaultAsync();
		}
	}
	public class RequestStreamResponderFrameHandler : FrameHandlerBase
	{
		RequestStream _message;
		ReadOnlySequence<byte> _metadata;
		ReadOnlySequence<byte> _data;

		IObserver<int> _requestNReceiver;
		IObservable<int> _requestNObservable;

		public RequestStreamResponderFrameHandler(RSocket socket, RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) : base(socket)
		{
			this._message = message;
			this._metadata = metadata;
			this._data = data;
			this.StreamId = message.Stream;

			var requestNObservable = Observable.Create<int>(observer =>
			{
				this._requestNReceiver = observer;
				this._requestNReceiver.OnNext(message.InitialRequest);
				return Disposable.Empty;
			});

			this._requestNObservable = requestNObservable;
		}

		public int StreamId { get; set; }

		protected override IObserver<int> GetRequestNHandler()
		{
			return _requestNReceiver;
		}

		public override void HandleCancel(Cancel message)
		{
			base.HandleCancel(message);
			this._requestNReceiver?.OnCompleted();
		}

		public override async Task AsTask()
		{
			var outgoing = this.Socket.Streamer((this._data, this._metadata));     //TODO Handle Errors.
																				   //outgoing = RSocket.MakeControllableStream(outgoing, this._requestNObservable);

			await Helpers.ForEach(outgoing,
				action: async value =>
				{
					await new RSocketProtocol.Payload(this.StreamId, value.Data, value.Metadata, next: true).WriteFlush(this.Socket.Transport.Output, value.Data, value.Metadata);
				},
				final: async () =>
				{
					await new RSocketProtocol.Payload(this.StreamId, complete: true).WriteFlush(this.Socket.Transport.Output);
					this._requestNReceiver.OnCompleted();
				}, cancel: this.CancelTrigger.Token);
		}
	}

	public class RequestChannelResponderFrameHandler : FrameHandlerBase
	{
		RequestChannel _message;
		ReadOnlySequence<byte> _metadata;
		ReadOnlySequence<byte> _data;

		IncomingStreamWrapper<PayloadContent> _incoming;
		IObservable<int> _requestNObservable;
		Task _incomingMonitorTask;
		IRSocketStream _incomingPayloadReceiver;
		IObserver<int> _requestNReceiver;

		OutputSubscriber<PayloadContent> _outputSubscriber;
		IDisposable _outputSubscriberSubscription;

		public RequestChannelResponderFrameHandler(RSocket socket, RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) : base(socket)
		{
			this._message = message;
			this._metadata = metadata;
			this._data = data;
			this.StreamId = message.Stream;

			IObserver<int> incomingMonitorObserver = null;
			IObservable<int> incomingMonitor = Observable.Create<int>(observer =>
			{
				incomingMonitorObserver = observer;
				return Disposable.Empty;
			});

			var incomingMonitorTask = incomingMonitor.ToAsyncEnumerable().LastOrDefaultAsync().AsTask();
			this._incomingMonitorTask = incomingMonitorTask;

			var inc = Observable.Create<PayloadContent>(observer =>
			{
				this._incomingPayloadReceiver = observer;

				return () =>
				{
					incomingMonitorObserver.OnCompleted();
				};
			});

			this._incoming = new IncomingStreamWrapper<PayloadContent>(inc, socket, message.Stream);

			var requestNObservable = Observable.Create<int>(observer =>
			{
				this._requestNReceiver = observer;
				this._requestNReceiver.OnNext(message.InitialRequest);
				return Disposable.Empty;
			});

			this._requestNObservable = requestNObservable;
		}

		public int StreamId { get; set; }

		protected override IRSocketStream GetPayloadHandler()
		{
			return this._incomingPayloadReceiver;
		}

		protected override IObserver<int> GetRequestNHandler()
		{
			return this._requestNReceiver;
		}

		public override void HandleCancel(Cancel message)
		{
			base.HandleCancel(message);

			this.StopOutging();
		}

		void StopIncoming()
		{
			this._incomingPayloadReceiver?.OnCompleted();
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
			var outgoing = this.Socket.Channeler1(new PayloadContent(this._data, this._metadata), this._incoming);     //TODO Handle Errors.

			OutputSubscriber<PayloadContent> outputSubscriber = null;
			IDisposable outputSubscriberSubscription = null;

			var outputStream = Observable.Create<PayloadContent>(observer =>
			  {
				  outputSubscriber = new OutputSubscriber<PayloadContent>(observer);
				  outputSubscriberSubscription = outgoing.Subscribe(outputSubscriber);

				  this._outputSubscriber = outputSubscriber;
				  this._outputSubscriberSubscription = outputSubscriberSubscription;

				  return Disposable.Empty;
			  });

			var outputPayloads = RSocket.MakeControllableStream(outputStream, this._requestNObservable);

			var outputTask = Helpers.ForEach(outputPayloads,
				action: async value =>
				{
					//Thread.Sleep(1000);
					Console.WriteLine($"发送服务端消息-{Thread.CurrentThread.ManagedThreadId}");
					await new RSocketProtocol.Payload(this.StreamId, value.Data, value.Metadata, next: true).WriteFlush(this.Socket.Transport.Output, value.Data, value.Metadata);
				},
				final: async () =>
				{
					await new RSocketProtocol.Payload(this.StreamId, complete: true).WriteFlush(this.Socket.Transport.Output);
					this._requestNReceiver.OnCompleted();
				}, cancel: this.CancelTrigger.Token);

			await Task.WhenAll(outputTask, this._incomingMonitorTask);
		}

		protected override void Dispose(bool disposing)
		{
			this.StopIncoming();
			this.StopOutging();
		}
	}
	public class RequestChannelRequesterFrameHandler : FrameHandlerBase
	{
		ReadOnlySequence<byte> _metadata;
		ReadOnlySequence<byte> _data;

		IObservable<PayloadContent> _outgoing;

		IObservable<int> _requestNObservable;
		IObserver<PayloadContent> _incomingPayloadReceiver;
		IObserver<int> _requestNReceiver;

		OutputSubscriber<PayloadContent> _outputSubscriber;
		IDisposable _outputSubscriberSubscription;

		public RequestChannelRequesterFrameHandler(RSocket socket
			, int streamId
			, IObserver<PayloadContent> incomingPayloadReceiver
			, ReadOnlySequence<byte> metadata
			, ReadOnlySequence<byte> data
			, IObservable<PayloadContent> outgoing) : base(socket)
		{
			this._metadata = metadata;
			this._data = data;
			this.StreamId = streamId;
			this._outgoing = outgoing;

			this._incomingPayloadReceiver = incomingPayloadReceiver;

			var requestNObservable = Observable.Create<int>(observer =>
			{
				this._requestNReceiver = observer;
				return Disposable.Empty;
			});

			this._requestNObservable = requestNObservable;
		}

		public int StreamId { get; set; }

		int _incomings = 0;
		protected override IObserver<PayloadContent> GetPayloadHandler()
		{
			_incomings++;
			Console.WriteLine(_incomings);
			return this._incomingPayloadReceiver;
		}

		protected override IObserver<int> GetRequestNHandler()
		{
			return this._requestNReceiver;
		}

		public override void HandleCancel(Cancel message)
		{
			base.HandleCancel(message);

			this.StopOutging();
		}

		void StopIncoming()
		{
			this._incomingPayloadReceiver?.OnCompleted();
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

			OutputSubscriber<PayloadContent> outputSubscriber = null;
			IDisposable outputSubscriberSubscription = null;

			var outputStream = Observable.Create<PayloadContent>(observer =>
			{
				outputSubscriber = new OutputSubscriber<PayloadContent>(observer);
				outputSubscriberSubscription = outgoing.Subscribe(outputSubscriber);

				this._outputSubscriber = outputSubscriber;
				this._outputSubscriberSubscription = outputSubscriberSubscription;

				return Disposable.Empty;
			});

			var outputPayloads = RSocket.MakeControllableStream(outputStream, this._requestNObservable);

			var outputTask = Helpers.ForEach(outputPayloads,
				action: async value =>
				{
					//Thread.Sleep(1000);
					//Console.WriteLine($"发送服务端消息-{Thread.CurrentThread.ManagedThreadId}");
					await new RSocketProtocol.Payload(this.StreamId, value.Data, value.Metadata, next: true).WriteFlush(this.Socket.Transport.Output, value.Data, value.Metadata);
				},
				final: async () =>
				{
					await new RSocketProtocol.Payload(this.StreamId, complete: true).WriteFlush(this.Socket.Transport.Output);
					this._requestNReceiver.OnCompleted();
				}, cancel: this.CancelTrigger.Token);

			await outputTask;
		}

		protected override void Dispose(bool disposing)
		{
			this.StopIncoming();
			this.StopOutging();
		}
	}
}
