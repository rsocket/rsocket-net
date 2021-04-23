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

		TaskCompletionSource<bool> _incomingCompletionWaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource<bool> _outgoingCompletionWaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		IncomingReceiver _incomingReceiver;

		Lazy<IPublisher<Payload>> _lazyOutgoing;
		Lazy<(ISubscription Subscription, IObserver<Payload> Subscriber)> _lazyOutgoingSubscriber;

		static readonly OperationCanceledException ChannelHasTerminatedException = new OperationCanceledException("Channel has terminated.");

		protected Channel(RSocket socket, int channelId)
		{
			this.Socket = socket;
			this.ChannelId = channelId;

			this._incomingReceiver = new IncomingReceiver();
			this.Incoming = this.CreateIncoming();
			this._lazyOutgoing = new Lazy<IPublisher<Payload>>(this.CreateOutgoingLazy, LazyThreadSafetyMode.ExecutionAndPublication);
			this._lazyOutgoingSubscriber = new Lazy<(ISubscription Subscription, IObserver<Payload> Subscriber)>(this.SubscribeOutgoing, LazyThreadSafetyMode.ExecutionAndPublication);
		}
		protected Channel(RSocket socket, int channelId, int initialOutgoingRequest) : this(socket, channelId)
		{
			this._initialOutgoingRequest = initialOutgoingRequest;
		}

		public RSocket Socket { get; set; }
		public int ChannelId { get; set; }

		public bool IncomingFinished { get; private set; }
		public bool OutgoingFinished { get; private set; }

		public IPublisher<Payload> Incoming { get; private set; }
		public IPublisher<Payload> Outgoing { get { return this._lazyOutgoing.Value; } }

		protected IObserver<Payload> IncomingSubscriber { get { return this._incomingReceiver; } }

		ISubscription _outgoingSubscription;
		ISubscription OutgoingSubscription { get { return this._lazyOutgoingSubscriber.Value.Subscription; } }

		protected virtual IPublisher<Payload> CreateIncoming()
		{
			return new IncomingStream(this._incomingReceiver, this);
		}
		protected virtual IPublisher<Payload> CreateOutgoing()
		{
			return new Publisher<Payload>();
		}

		public void FinishIncoming()
		{
			if (this.IncomingFinished)
				return;

			this.IncomingFinished = true;

			this._incomingReceiver.Dispose();
			this._incomingCompletionWaiter.TrySetResult(true);
		}
		public void FinishOutgoing()
		{
			if (this.OutgoingFinished)
				return;

			this.OutgoingFinished = true;

			this._outgoingSubscription?.Dispose();
			this._outgoingCompletionWaiter.TrySetResult(true);
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
				return new Publisher<Payload>();
			}
		}
		(ISubscription Subscription, IObserver<Payload> Subscriber) SubscribeOutgoing()
		{
			var subscriber = new DefaultOutgoingSubscriber(this);
			var subscription = this.Outgoing.Subscribe(subscriber);
			this._outgoingSubscription = subscription;

			if (this.OutgoingFinished) // In case another thread finishes outgoing
				this._outgoingSubscription.Dispose();

			return (subscription, subscriber);
		}

		object EnsureHaveBeenReady()
		{
			return this.OutgoingSubscription;
		}


		public virtual void OnOutgoingNext(Payload payload)
		{
			if (this.OutgoingFinished)
				return;

			this.Socket.SendPayload(this.ChannelId, data: payload.Data, metadata: payload.Metadata, complete: false, next: true);
		}
		public virtual void OnOutgoingCompleted()
		{
			if (this.OutgoingFinished)
				return;

			this.Socket.SendPayload(this.ChannelId, complete: true, next: false);
			this.FinishOutgoing();
		}
		public virtual void OnOutgoingError(Exception error)
		{
			this.FinishOutgoing();
			this.NotifyIncomingSubscriberError(new OperationCanceledException("Outbound has terminated with an error.", error));
			this.FinishIncoming();
			this.Socket.SendError(this.ChannelId, ErrorCodes.Application_Error, $"{error.Message}\n{error.StackTrace}");
		}


		public virtual void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.EnsureHaveBeenReady();
			this.HandlePayloadCore(message, metadata, data);
		}
		protected virtual void HandlePayloadCore(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			if (this.IncomingFinished)
				return;

			var incomingReceiver = this._incomingReceiver;

			if (message.IsNext)
			{
				incomingReceiver.OnNext(new Payload(data, metadata));
			}

			if (message.IsComplete)
			{
				incomingReceiver.OnCompleted();
				this.FinishIncoming();
			}
		}

		public virtual void HandleError(RSocketProtocol.Error message)
		{
			this.EnsureHaveBeenReady();
			this.HandleErrorCore(message);
		}
		protected virtual void HandleErrorCore(RSocketProtocol.Error message)
		{
			this.FinishOutgoing();
			this._incomingReceiver.OnError(message.MakeException());
			this.FinishIncoming();
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
			if (this.OutgoingFinished)
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


		public virtual void OnIncomingSubscriberOnNextError()
		{
			this.OnIncomingSubscriptionCanceled();
		}
		public virtual void OnIncomingSubscriptionCanceled()
		{
			if (this.IncomingFinished)
				return;

			this.FinishIncoming();
			this.SendCancelFrame();
		}
		public void OnIncomingSubscriptionRequestN(int n)
		{
			if (this.IncomingFinished)
				return;

			this.Socket.SendRequestN(this.ChannelId, n);
		}
		internal void SendCancelFrame()
		{
#if DEBUG
			Console.WriteLine($"Sending cancel frame...............stream[{this.ChannelId}]");
#endif
			this.Socket.SendCancel(this.ChannelId);
		}


		public virtual async Task ToTask()
		{
			if (this._initialOutgoingRequest > 0)
				this.HandleRequestN(this._initialOutgoingRequest);
			await Task.WhenAll(this._incomingCompletionWaiter.Task, this._outgoingCompletionWaiter.Task);
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this.NotifyIncomingSubscriberError(ChannelHasTerminatedException);
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
		void NotifyIncomingSubscriberError(Exception ex)
		{
			try
			{
				/* Incoming subscriber will receive an error if the incoming is unfinished. */
				this.IncomingSubscriber.OnError(ex);
			}
			catch
			{
			}
		}
	}
}
