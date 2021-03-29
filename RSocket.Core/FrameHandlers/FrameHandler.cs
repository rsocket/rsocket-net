using RSocket.Exceptions;
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
using static RSocket.RSocketProtocol;

namespace RSocket
{
	public abstract class FrameHandler : IFrameHandler
	{
		bool _disposed = false;
		int _initialOutputRequest = 0;

		TaskCompletionSource<bool> _waitIncomingCompleteHandler = new TaskCompletionSource<bool>();
		TaskCompletionSource<bool> _waitOutgoingCompleteHandler = new TaskCompletionSource<bool>();

		CancellationTokenSource _inputCts = new CancellationTokenSource();
		CancellationTokenSource _outputCts = new CancellationTokenSource();

		IncomingReceiver _incomingReceiver = new IncomingReceiver();
		bool _incomingCompleted;

		Lazy<IPublisher<Payload>> _lazyOutgoing;
		Lazy<(ISubscription Subscription, IObserver<Payload> Subscriber)> _lazyOutgoingSubscriber;

		protected FrameHandler(RSocket socket)
		{
			this.Socket = socket;
			this._inputCts.Token.Register(this.StopIncoming);
			this._outputCts.Token.Register(this.StopOutging);

			this.Incoming = this.CreateIncoming();
			this._lazyOutgoing = new Lazy<IPublisher<Payload>>(this.CreateOutgingLazy, LazyThreadSafetyMode.ExecutionAndPublication);
			this._lazyOutgoingSubscriber = new Lazy<(ISubscription Subscription, IObserver<Payload> Subscriber)>(this.SubscribeOutgoing, LazyThreadSafetyMode.ExecutionAndPublication);
		}
		protected FrameHandler(RSocket socket, int streamId) : this(socket)
		{
			this.StreamId = streamId;
		}

		protected FrameHandler(RSocket socket, int streamId, int initialOutputRequest) : this(socket, streamId)
		{
			this._initialOutputRequest = initialOutputRequest;
		}

		public RSocket Socket { get; set; }
		public int StreamId { get; set; }

		public IPublisher<Payload> Incoming { get; private set; }
		public IPublisher<Payload> Outgoing { get { return this._lazyOutgoing.Value; } }

		protected virtual IPublisher<Payload> CreateIncoming()
		{
			return new IncomingStream(this._incomingReceiver, this);
		}
		protected virtual IPublisher<Payload> CreateOutging()
		{
			return new SimplePublisher<Payload>();
		}
		IPublisher<Payload> CreateOutgingLazy()
		{
			try
			{
				return this.CreateOutging();
			}
			catch (Exception ex)
			{
				this.OnOutgoingError(ex);
				return new SimplePublisher<Payload>();
			}
		}

		public CancellationTokenSource OutputCts { get { return this._outputCts; } }

		protected IObserver<Payload> IncomingSubscriber { get { return this._incomingReceiver; } }

		IObserver<Payload> _outgoingSubscriber;
		ISubscription _outgoingSubscription;
		protected IObserver<Payload> OutgoingSubscriber { get { return this._lazyOutgoingSubscriber.Value.Subscriber; } }
		protected ISubscription OutgoingSubscription { get { return this._lazyOutgoingSubscriber.Value.Subscription; } }
		protected virtual bool OutputSingle { get { return false; } }

		(ISubscription Subscription, IObserver<Payload> Subscriber) SubscribeOutgoing()
		{
			var subscriber = new DefaultOutgoingSubscriber(this);
			var subscription = subscriber.Subscribe(this.Outgoing);
			this._outgoingSubscription = subscription;
			this._outgoingSubscriber = subscriber;

			if (this._outputCts.IsCancellationRequested)
				this._outgoingSubscription.Dispose();

			return (subscription, subscriber);
		}
		void OnOutgoingError(Exception error)
		{
			this.IncomingSubscriber?.OnError(new OperationCanceledException("Outbound has terminated with an error.", error));
			this.CancelInput();
			this.Socket.SendError(ErrorCodes.Application_Error, this.StreamId, $"{error.Message}\n{error.StackTrace}").Wait();
			this.OutputCompleted();
		}

		internal void CancelInput()
		{
			if (this._disposed)
				return;

			if (!this._inputCts.IsCancellationRequested)
				this._inputCts.Cancel();
		}
		internal void CancelOutput()
		{
			if (this._disposed)
				return;

			if (!this._outputCts.IsCancellationRequested)
				this._outputCts.Cancel();
		}
		protected void StopIncoming()
		{
			//this.IncomingSubscriber?.OnCompleted();
			this._incomingReceiver.Dispose();
			this.OnIncomingCompleted();
		}
		protected void StopOutging()
		{
			//cancel sending payload.
			this._outgoingSubscription?.Dispose();
			this.OutputCompleted();
		}
		protected void IncomingCompleted()
		{
			this._waitIncomingCompleteHandler.TrySetResult(true);
		}
		protected void OutputCompleted()
		{
			this._waitOutgoingCompleteHandler.TrySetResult(true);
		}

		public virtual void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			var incomingSubscriber = this.IncomingSubscriber;

			if (message.IsNext)
			{
				incomingSubscriber.OnNext(new Payload(data, metadata));
			}

			if (message.IsComplete)
			{
				incomingSubscriber.OnCompleted();
			}
		}

		public virtual void HandleRequestN(RSocketProtocol.RequestN message)
		{
			this.HandleRequestN(message.RequestNumber);
		}
		internal void HandleRequestN(int n)
		{
			if (this._outputCts.IsCancellationRequested)
				return;

			this.OutgoingSubscription.Request(n);
		}


		public virtual void HandleCancel(RSocketProtocol.Cancel message)
		{
#if DEBUG
			Console.WriteLine($"frameHandler.HandleCancel() {this.OutputCts.Token.IsCancellationRequested}");
#endif

			this.CancelOutput();
		}

		public virtual void HandleError(RSocketProtocol.Error message)
		{
			this.CancelOutput();
			this.IncomingSubscriber?.OnError(message.MakeException());
			this.CancelInput();
		}

		//called by InboundSubscription.
		internal virtual void OnIncomingCompleted()
		{
			this._incomingCompleted = true;
			this.IncomingCompleted();
		}
		//called by InboundSubscription.
		internal virtual void OnIncomingCanceled()
		{
			if (this._incomingCompleted)
				return;

			this.SendCancelFrame();
			this.OnIncomingCompleted();
		}
		void SendCancelFrame()
		{
			if (!this._inputCts.IsCancellationRequested)
			{
				this.Socket.SendCancel(this.StreamId).Wait();
			}
		}
		//called by InboundSubscription.
		internal void OnRequestN(int n)
		{
			if (!this._inputCts.IsCancellationRequested)
			{
				this.Socket.SendRequestN(this.StreamId, n).Wait();
			}
		}

		public virtual async Task ToTask()
		{
			if (this._initialOutputRequest > 0)
				this.HandleRequestN(this._initialOutputRequest);
			await Task.WhenAll(this._waitIncomingCompleteHandler.Task, this._waitOutgoingCompleteHandler.Task);
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this.CancelInput();
			this.CancelOutput();

			this._inputCts.Dispose();
			this._outputCts.Dispose();

			this.Dispose(true);

			this._disposed = true;
		}

		protected virtual void Dispose(bool disposing)
		{

		}


		class DefaultOutgoingSubscriber : Subscriber<Payload>, IObserver<Payload>
		{
			FrameHandler _frameHandler;

			public DefaultOutgoingSubscriber(FrameHandler frameHandler)
			{
				this._frameHandler = frameHandler;
			}

			protected override void DoOnCompleted()
			{
				if (!this._frameHandler.OutputSingle)
				{
					this._frameHandler.Socket.SendPayload(default(Payload), this._frameHandler.StreamId, true, false).Wait();
				}
				this._frameHandler.OutputCompleted();
			}

			protected override void DoOnError(Exception error)
			{
				this._frameHandler.OnOutgoingError(error);
			}

			protected override void DoOnNext(Payload value)
			{
				if (this._frameHandler.OutputSingle)
				{
					this._frameHandler.Socket.SendPayload(value, this._frameHandler.StreamId, true, true).Wait();
					return;
				}

				this._frameHandler.Socket.SendPayload(value, this._frameHandler.StreamId, false, true).Wait();
			}
		}
	}
}
