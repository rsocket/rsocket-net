using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using IRSocketStream = System.IObserver<RSocket.Payload>;
using System.Reactive.Disposables;
using RSocket.Exceptions;

namespace RSocket
{
	public partial class RSocket : IRSocketProtocol, IDisposable
	{
		bool _disposed;
		DateTime _lastKeepAliveReceived = DateTime.Now;
		IDisposable _ticksDisposable = Disposable.Empty;

		int _connectionClosedFlag = 0;
		internal SemaphoreSlim OutputSyncLock = new SemaphoreSlim(1, 1);

		PrefetchOptions Options { get; set; }

		public bool IsClosed { get { return this._connectionClosedFlag != 0; } }

		//TODO Hide.
		public IRSocketTransport Transport { get; set; }

		int StreamId = 1 - 2;
		protected internal virtual int NewStreamId() => Interlocked.Add(ref StreamId, 2);

		ConcurrentDictionary<int, IChannel> _activeChannels = new ConcurrentDictionary<int, IChannel>();
		internal void AddChannel(IChannel channel)
		{
			this._activeChannels[channel.ChannelId] = channel;
		}
		internal IChannel RemoveChannel(int id)
		{
			this._activeChannels.TryRemove(id, out var channel);
			return channel;
		}
		internal void RemoveAndReleaseChannel(int id)
		{
			IChannel channel = this.RemoveChannel(id);
			if (channel != null)
			{
				try
				{
					channel.Dispose();
				}
				catch
				{
				}
			}
		}

		public RSocket(IRSocketTransport transport, PrefetchOptions options = default)
		{
			Transport = transport;
			Options = options ?? PrefetchOptions.Default;
		}

		/// <summary>Binds the RSocket to its Transport and begins handling messages.</summary>
		/// <param name="cancel">Cancellation for the handler. Requesting cancellation will stop message handling.</param>
		/// <returns>The handler task.</returns>
		public Task Connect(CancellationToken cancel = default) => this.Handler(Transport.Input, cancel);

		public virtual void OnClose()
		{

		}

		public virtual async Task RequestFireAndForget(ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		{
			this.CheckConnectionStatus();
			Func<int, Task> channelEstablisher = async streamId =>
			{
				await this.SendRequestFireAndForget(streamId, data, metadata);
			};

			IPublisher<Payload> incoming = new RequestFireAndForgetRequesterIncomingStream(this, channelEstablisher);
			incoming.Subscribe();
			await Task.CompletedTask;
		}

		public virtual async Task<Payload> RequestResponse(ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		{
			this.CheckConnectionStatus();
			Func<int, Task> channelEstablisher = async streamId =>
			{
				await this.SendRequestResponse(streamId, data, metadata);
			};

			IPublisher<Payload> incoming = new RequestResponseRequesterIncomingStream(this, channelEstablisher);
			//var ret = await incoming.ToAsyncEnumerable().FirstAsync(); //Cannot use linq operation to get the payload, because of it will produces redundant cancel frame. ps: 'ISubscription.Dispose()' will be executed before 'IObserver<T>.OnComplete()' when using 'IObservable<T>.ToAsyncEnumerable().FirstAsync()' method.

			TaskCompletionSource<Payload> taskSignal = new TaskCompletionSource<Payload>(TaskCreationOptions.RunContinuationsAsynchronously);

			var sub = incoming.Subscribe(a =>
			{
				taskSignal.TrySetResult(a);
			}, error =>
			{
				taskSignal.TrySetException(error);
			}, () =>
			{
				if (taskSignal.Task.Status != TaskStatus.RanToCompletion)
					taskSignal.TrySetCanceled();
			});

			try
			{
				return await taskSignal.Task;
			}
			finally
			{
				sub.Dispose();
			}
		}
		[Obsolete("This method has obsoleted.")]
		public virtual Task<T> RequestResponse<T>(Func<Payload, T> resultmapper,
		ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		=> throw new NotSupportedException("This method has obsoleted.");

		public IPublisher<Payload> RequestStream(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata, int initial = RSocketOptions.INITIALDEFAULT)
		{
			this.CheckConnectionStatus();
			Func<int, Task> channelEstablisher = async streamId =>
			{
				await this.SendRequestStream(streamId, data, metadata, initialRequest: this.Options.GetInitialRequestSize(initial));
			};

			var incoming = new RequestStreamRequesterIncomingStream(this, channelEstablisher);
			return incoming;
		}
		[Obsolete("This method has obsoleted.")]
		public virtual IAsyncEnumerable<T> RequestStream<T>(Func<Payload, T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> throw new NotSupportedException("This method has obsoleted.");
		[Obsolete("This method has obsoleted.")]
		public Task RequestStream(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			throw new NotSupportedException("This method has obsoleted.");
		}

		public IPublisher<Payload> RequestChannel(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata, IPublisher<Payload> source, int initial = RSocketOptions.INITIALDEFAULT)
		{
			return this.RequestChannel(data, metadata, (IObservable<Payload>)source, initial);
		}
		public IPublisher<Payload> RequestChannel(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata, IObservable<Payload> source, int initial = RSocketOptions.INITIALDEFAULT)
		{
			/*
			 * A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.
			 */

			this.CheckConnectionStatus();

			Func<int, Task> channelEstablisher = async streamId =>
			{
				await this.SendRequestChannel(streamId, data, metadata, initialRequest: this.Options.GetInitialRequestSize(initial));
			};

			var incoming = new RequesterIncomingStream(this, source, channelEstablisher);
			return incoming;
		}
		[Obsolete("This method has obsoleted.")]
		public virtual IAsyncEnumerable<T> RequestChannel<TSource, T>(IAsyncEnumerable<TSource> source, Func<TSource, ReadOnlySequence<byte>> sourcemapper,
			Func<Payload, T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> throw new NotSupportedException("This method has obsoleted.");

		internal void StartKeepAlive(TimeSpan keepAliveInterval, TimeSpan keepAliveTimeout)
		{
			this._lastKeepAliveReceived = DateTime.Now;

			if (!(keepAliveInterval.TotalMilliseconds > 0 && keepAliveTimeout.TotalMilliseconds > 0))
				return;

			var timer = Observable.Timer(keepAliveInterval, keepAliveInterval);
			this._ticksDisposable = timer.Subscribe(a =>
			  {
				  this.KeepAliveIntervalTick(keepAliveTimeout);
			  });
		}
		void KeepAliveIntervalTick(TimeSpan keepAliveTimeout)
		{
			if ((DateTime.Now - this._lastKeepAliveReceived).TotalMilliseconds > keepAliveTimeout.TotalMilliseconds)
			{
				//timeout
				this._ticksDisposable.Dispose();
				this.CloseConnection();

				string errorText = $"No keep-alive acks for {keepAliveTimeout.TotalMilliseconds} ms.";

#if DEBUG
				Console.WriteLine(errorText);
#endif

				this.ReleaseAllChannels(RSocketProtocol.ErrorCodes.Connection_Error, errorText);
				return;
			}

			this.SendKeepAlive(0, true);
		}

		void ReleaseAllChannels(RSocketProtocol.ErrorCodes errorCode, string errorText)
		{
			var errorData = Helpers.StringToByteSequence(errorText);
			lock (this)
			{
				foreach (var kv in this._activeChannels)
				{
					var channel = kv.Value;
					int channelId = channel.ChannelId;
					try
					{
						channel.HandleError(new RSocketProtocol.Error(errorCode, channelId, errorData, errorText));
						this.RemoveChannel(channelId);
						channel.Dispose();
					}
					catch
					{
					}
				}
			}
		}

		internal void Schedule(int stream, Func<int, CancellationToken, Task> operation, CancellationToken cancel = default)
		{
			var task = operation(stream, cancel);
			if (!task.IsCompleted)
			{
				task.ConfigureAwait(false); //FUTURE Someday might want to schedule these in a different pool or perhaps track all in-flight tasks.
			}
		}

		void CheckConnectionStatus()
		{
			if (this.IsClosed)
				throw new ConnectionCloseException("The connection has been closed.");

			if (this._disposed)
				throw new ObjectDisposedException(this.GetType().FullName);
		}

		void CloseConnection()
		{
			var _ = this.CloseConnectionAsync();
		}
		async Task CloseConnectionAsync()
		{
			if (Interlocked.CompareExchange(ref this._connectionClosedFlag, 1, 0) != 0)
				return;

			await this.Transport.StopAsync();
			this.OnClose();
		}
		public void Dispose()
		{
			if (this._disposed)
				return;

			this._ticksDisposable.Dispose();
			this.CloseConnection();
			this.ReleaseAllChannels(RSocketProtocol.ErrorCodes.Connection_Close, null);
			this.OutputSyncLock.Dispose();

			this.Dispose(true);

			this._disposed = true;
		}
		protected virtual void Dispose(bool disposing)
		{

		}
	}
}
