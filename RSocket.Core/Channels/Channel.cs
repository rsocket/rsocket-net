using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using static RSocket.RSocketProtocol;

namespace RSocket.Channels
{
	public abstract partial class Channel : IChannel
	{
		bool _disposed = false;
		int _initialOutgoingRequest = 0;

		TaskCompletionSource<bool> _waitIncomingCompleteHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource<bool> _waitOutgoingCompleteHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		IncomingReceiver _incomingReceiver;

		Lazy<IPublisher<Payload>> _lazyOutgoing;
		Lazy<(ISubscription Subscription, IObserver<Payload> Subscriber)> _lazyOutgoingSubscriber;

		bool _incomingFinished;
		bool _outgoingFinished;

		public bool IncomingFinished { get { return this._incomingFinished; } }
		public bool OutgoingFinished { get { return this._outgoingFinished; } }

		Channel(RSocket socket)
		{
			this.Socket = socket;

			this._incomingReceiver = new IncomingReceiver(this);
			this.Incoming = this.CreateIncoming();
			this._lazyOutgoing = new Lazy<IPublisher<Payload>>(this.CreateOutgoingLazy, LazyThreadSafetyMode.ExecutionAndPublication);
			this._lazyOutgoingSubscriber = new Lazy<(ISubscription Subscription, IObserver<Payload> Subscriber)>(this.SubscribeOutgoing, LazyThreadSafetyMode.ExecutionAndPublication);
		}
		protected Channel(RSocket socket, int channelId) : this(socket)
		{
			this.ChannelId = channelId;
		}
		protected Channel(RSocket socket, int channelId, int initialOutgoingRequest) : this(socket, channelId)
		{
			this._initialOutgoingRequest = initialOutgoingRequest;
		}

		public RSocket Socket { get; set; }
		public int ChannelId { get; set; }

		public IPublisher<Payload> Incoming { get; private set; }
		public IPublisher<Payload> Outgoing { get { return this._lazyOutgoing.Value; } }

		protected IObserver<Payload> IncomingSubscriber { get { return this._incomingReceiver; } }

		ISubscription _outgoingSubscription;
		protected IObserver<Payload> OutgoingSubscriber { get { return this._lazyOutgoingSubscriber.Value.Subscriber; } }
		protected ISubscription OutgoingSubscription { get { return this._lazyOutgoingSubscriber.Value.Subscription; } }
		protected virtual bool OutputSingle { get { return false; } }

		protected virtual IPublisher<Payload> CreateIncoming()
		{
			return new IncomingStream(this._incomingReceiver, this);
		}
		protected virtual IPublisher<Payload> CreateOutgoing()
		{
			return new SimplePublisher<Payload>();
		}
		IPublisher<Payload> CreateOutgoingLazy()
		{
			try
			{
				return this.CreateOutgoing();
			}
			catch (Exception ex)
			{
				this.OnOutgoingError(ex);
				return new SimplePublisher<Payload>();
			}
		}
		(ISubscription Subscription, IObserver<Payload> Subscriber) SubscribeOutgoing()
		{
			var subscriber = new DefaultOutgoingSubscriber(this);
			var subscription = subscriber.Subscribe(this.Outgoing);
			this._outgoingSubscription = subscription;

			if (this._outgoingFinished) // In case another thread finishes outgoing
				this._outgoingSubscription.Dispose();

			return (subscription, subscriber);
		}
		void OnOutgoingError(Exception error)
		{
			this.FinishOutgoing();
			this.IncomingSubscriber.OnError(new OperationCanceledException("Outbound has terminated with an error.", error));
			this.Socket.SendError(this.ChannelId, ErrorCodes.Application_Error, $"{error.Message}\n{error.StackTrace}").Wait();
		}
		object EnsureHaveBeenReady()
		{
			return this.OutgoingSubscription;
		}

		public void FinishIncoming()
		{
			if (this._incomingFinished)
				return;

			this._incomingFinished = true;

			this._incomingReceiver.Dispose();
			this._waitIncomingCompleteHandler.TrySetResult(true);
		}
		public void FinishOutgoing()
		{
			if (this._outgoingFinished)
				return;

			this._outgoingFinished = true;

			this._outgoingSubscription?.Dispose();
			this._waitOutgoingCompleteHandler.TrySetResult(true);
		}

		public virtual void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.EnsureHaveBeenReady();
			this.HandlePayloadCore(message, metadata, data);
		}
		protected virtual void HandlePayloadCore(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			if (this._incomingFinished)
				return;

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
		public virtual void HandleRequestN(int n)
		{
			this.EnsureHaveBeenReady();
			this.HandleRequestNCore(n);
		}
		protected virtual void HandleRequestNCore(int n)
		{
			this.EnsureHaveBeenReady();
			if (this._outgoingFinished)
				return;

			this.OutgoingSubscription.Request(n);
		}

		public virtual void HandleCancel(RSocketProtocol.Cancel message)
		{
#if DEBUG
			Console.WriteLine($"Handling cancel message...............stream[{this.ChannelId}]");
#endif
			this.EnsureHaveBeenReady();
			this.HandleCancelCore();
		}
		protected virtual void HandleCancelCore()
		{
			this.FinishOutgoing();
		}

		public virtual void HandleError(RSocketProtocol.Error message)
		{
			this.EnsureHaveBeenReady();
			this.HandleErrorCore(message);
		}
		protected virtual void HandleErrorCore(RSocketProtocol.Error message)
		{
			this.FinishOutgoing();
			this.IncomingSubscriber.OnError(message.MakeException());
		}

		//called by InboundSubscription.
		public virtual void OnIncomingCompleted()
		{
			this.FinishIncoming();
		}
		//called by InboundSubscription.
		public virtual void OnIncomingCanceled()
		{
			if (this._incomingFinished)
				return;

			this.FinishIncoming();
			this.SendCancelFrame();
		}
		internal void SendCancelFrame()
		{
#if DEBUG
			Console.WriteLine($"Sending cancel frame...............stream[{this.ChannelId}]");
#endif
			this.Socket.SendCancel(this.ChannelId).Wait();
		}
		//called by InboundSubscription.
		public void OnIncomingSubscriberRequestN(int n)
		{
			if (this._incomingFinished)
				return;

			this.Socket.SendRequestN(this.ChannelId, n).Wait();
		}

		public virtual async Task ToTask()
		{
			if (this._initialOutgoingRequest > 0)
				this.HandleRequestN(this._initialOutgoingRequest);
			await Task.WhenAll(this._waitIncomingCompleteHandler.Task, this._waitOutgoingCompleteHandler.Task);
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this.TriggerErrorIfIncomingUnfinished();
			this.FinishIncoming();
			this.FinishOutgoing();

			try
			{
				this.Dispose(true);
			}
			catch
			{
			}

			this._disposed = true;
		}
		protected virtual void Dispose(bool disposing)
		{
		}
		void TriggerErrorIfIncomingUnfinished()
		{
			if (this._incomingFinished)
				return;

			try
			{
				this.IncomingSubscriber.OnError(new OperationCanceledException("Channel has terminated."));
			}
			catch
			{
			}
		}
	}
}
