using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using IRSocketStream = System.IObserver<RSocket.Payload>;
using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;
using RSocket.Exceptions;

namespace RSocket
{
	public interface IRSocketChannel
	{
		Task Send(Payload value);
		Task Complete();
	}

	public partial class RSocket : IRSocketProtocol, IDisposable
	{
		bool _disposed;
		bool _connectionClosed;
		DateTime _lastKeepAliveReceived = DateTime.Now;
		IDisposable _ticksDisposable = Disposable.Empty;

		PrefetchOptions Options { get; set; }

		//TODO Hide.
		public IRSocketTransport Transport { get; set; }
		private int StreamId = 1 - 2;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
		protected internal virtual int NewStreamId() => Interlocked.Add(ref StreamId, 2);  //TODO SPEC: To reuse or not... Should tear down the client if this happens or have to skip in-use IDs.

		private ConcurrentDictionary<int, IRSocketStream> Dispatcher = new ConcurrentDictionary<int, IRSocketStream>();
		private int StreamDispatch(int id, IRSocketStream transform) { Dispatcher[id] = transform; return id; }
		private int StreamDispatch(IRSocketStream transform) => StreamDispatch(NewStreamId(), transform);
		private IRSocketStream StreamRemove(int id)
		{
			this.Dispatcher.TryRemove(id, out var transform);
			return transform;
		}


		private ConcurrentDictionary<int, IFrameHandler> FrameHandlerDispatcher = new ConcurrentDictionary<int, IFrameHandler>();
		internal int FrameHandlerDispatch(int id, IFrameHandler frameHandler) { FrameHandlerDispatcher[id] = frameHandler; return id; }
		private int FrameHandlerDispatch(IFrameHandler frameHandler) => FrameHandlerDispatch(NewStreamId(), frameHandler);
		internal IFrameHandler FrameHandlerRemove(int id)
		{
			this.FrameHandlerDispatcher.TryRemove(id, out var frameHandler);
			return frameHandler;
		}

		//TODO Stream Destruction - i.e. removal from the dispatcher.

		protected IDisposable ChannelSubscription;      //TODO Tracking state for channels

		public RSocket(IRSocketTransport transport, PrefetchOptions options = default)
		{
			Transport = transport;
			Options = options ?? PrefetchOptions.Default;
		}

		/// <summary>Binds the RSocket to its Transport and begins handling messages.</summary>
		/// <param name="cancel">Cancellation for the handler. Requesting cancellation will stop message handling.</param>
		/// <returns>The handler task.</returns>
		public Task Connect(CancellationToken cancel = default) => this.Handler(Transport.Input, cancel);

		public virtual async Task RequestFireAndForget(ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		{
			this.CheckConnectionStatus();
			var streamId = this.NewStreamId();
			await new RSocketProtocol.RequestFireAndForget(streamId, data, metadata).WriteFlush(this.Transport.Output, data, metadata);
		}

		[Obsolete("This method has obsoleted.")]
		public virtual Task<T> RequestResponse<T>(Func<Payload, T> resultmapper,
		ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		=> new Receiver<T>(stream => RequestResponse(stream, data, metadata), resultmapper).ExecuteAsync();
		[Obsolete("This method has obsoleted.")]
		public Task RequestResponse(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
		{
			var id = StreamDispatch(stream);
			return new RSocketProtocol.RequestResponse(id, data, metadata).WriteFlush(Transport.Output, data, metadata);
		}
		public virtual async Task<Payload> RequestResponse(ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		{
			this.CheckConnectionStatus();
			Func<int, Task> channelEstablisher = async streamId =>
			{
				await new RSocketProtocol.RequestResponse(streamId, data, metadata).WriteFlush(Transport.Output, data, metadata);
			};

			var incoming = new RequestResponseRequesterIncomingStream(this, channelEstablisher);
			//var ret = await incoming.ToAsyncEnumerable().FirstAsync(); //Cannot use linq operation to get the payload, because of it will produces redundant cancel frame. ps: 'ISubscription.Dispose()' will be executed before 'IObserver<T>.OnComplete()' when using 'IObservable<T>.ToAsyncEnumerable().FirstAsync()' method.

			TaskCompletionSource<bool> taskSignal = new TaskCompletionSource<bool>();
			Payload ret = default(Payload);
			ISubscription sub = incoming.Subscribe(a =>
			{
				ret = a;
				//taskSignal.TrySetResult(true); //Cannot set result at here in case sending redundant cancel frame.
			}, error =>
			{
				taskSignal.TrySetException(error);
			}, () =>
			{
				taskSignal.TrySetResult(true);
			});

			try
			{
				await taskSignal.Task;
				return ret;
			}
			finally
			{
				sub.Dispose();
			}
		}

		[Obsolete("This method has obsoleted.")]
		public virtual IAsyncEnumerable<T> RequestStream<T>(Func<Payload, T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<T>(stream => RequestStream(stream, data, metadata), value => resultmapper(value));
		[Obsolete("This method has obsoleted.")]
		public Task RequestStream(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var id = StreamDispatch(stream);
			return new RSocketProtocol.RequestStream(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata);
		}
		public IPublisher<Payload> RequestStream(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata, int initial = RSocketOptions.INITIALDEFAULT)
		{
			this.CheckConnectionStatus();
			Func<int, Task> channelEstablisher = async streamId =>
			{
				var channel = new RSocketProtocol.RequestStream(streamId, data, metadata, initialRequest: this.Options.GetInitialRequestSize(initial));
				await channel.WriteFlush(this.Transport.Output, data, metadata);
			};

			var incoming = new RequestStreamRequesterIncomingStream(this, channelEstablisher);
			return incoming;
		}

		//TODO SPEC: A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.
		[Obsolete("This method has obsoleted.")]
		public virtual IAsyncEnumerable<T> RequestChannel<TSource, T>(IAsyncEnumerable<TSource> source, Func<TSource, ReadOnlySequence<byte>> sourcemapper,
			Func<Payload, T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<TSource, T>(stream => RequestChannel(stream, data, metadata), source, _ => new Payload(default, sourcemapper(_)), value => resultmapper(value));
		[Obsolete("This method has obsoleted.")]
		public async Task<IRSocketChannel> RequestChannel(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var id = StreamDispatch(stream);
			await new RSocketProtocol.RequestChannel(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata);
			var channel = new ChannelHandler(this, id);
			return channel;
		}
		public IPublisher<Payload> RequestChannel(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata, IObservable<Payload> source, int initial = RSocketOptions.INITIALDEFAULT)
		{
			/*
			 * A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.
			 */

			this.CheckConnectionStatus();

			Func<int, Task> channelEstablisher = async streamId =>
			{
				var channel = new RSocketProtocol.RequestChannel(streamId, data, metadata, initialRequest: this.Options.GetInitialRequestSize(initial));
				await channel.WriteFlush(this.Transport.Output, data, metadata);
			};

			var incoming = new RequesterIncomingStream(this, source, channelEstablisher);
			return incoming;
		}

		protected internal class ChannelHandler : IRSocketChannel       //TODO hmmm...
		{
			readonly RSocket Socket;
			readonly int Stream;

			public ChannelHandler(RSocket socket, int stream) { Socket = socket; Stream = stream; }

			public Task Send(Payload value)
			{
				if (!Socket.FrameHandlerDispatcher.ContainsKey(Stream)) { throw new InvalidOperationException("Channel is closed"); }
				return new RSocketProtocol.Payload(Stream, value.Data, value.Metadata, next: true).WriteFlush(Socket.Transport.Output, value.Data, value.Metadata);
				//if (!Socket.Dispatcher.ContainsKey(Stream)) { throw new InvalidOperationException("Channel is closed"); }
				//return new RSocketProtocol.Payload(Stream, value.data, value.metadata, next: true).WriteFlush(Socket.Transport.Output, value.data, value.metadata);
			}

			public Task Complete()
			{
				if (!Socket.FrameHandlerDispatcher.ContainsKey(Stream)) { throw new InvalidOperationException("Channel is closed"); }
				return new RSocketProtocol.Payload(Stream, complete: true).WriteFlush(Socket.Transport.Output);
				//if (!Socket.Dispatcher.ContainsKey(Stream)) { throw new InvalidOperationException("Channel is closed"); }
				//return new RSocketProtocol.Payload(Stream, complete: true).WriteFlush(Socket.Transport.Output);
			}
		}

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
				this.CloseConnection().Wait();

				string errorText = $"No keep-alive acks for {keepAliveTimeout.TotalMilliseconds} ms.";

#if DEBUG
				Console.WriteLine(errorText);
#endif

				this.ReleaseAllFrameHandlers(RSocketProtocol.ErrorCodes.Connection_Error, errorText);
				return;
			}

			RSocketProtocol.KeepAlive keepAlive = new RSocketProtocol.KeepAlive(0, true);
			keepAlive.WriteFlush(this.Transport.Output);
		}

		void ReleaseAllFrameHandlers(RSocketProtocol.ErrorCodes errorCode, string errorText)
		{
			var errorData = Helpers.StringToByteSequence(errorText);
			lock (this)
			{
				foreach (var kv in this.FrameHandlerDispatcher)
				{
					int streamId = kv.Key;
					var frameHandler = kv.Value;
					try
					{
						frameHandler.HandleError(new RSocketProtocol.Error(errorCode, streamId, errorData, errorText));
						this.FrameHandlerRemove(streamId);
						frameHandler.Dispose();
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
			if (this._connectionClosed)
				throw new ConnectionCloseException("The connection has been closed.");

			if (this._disposed)
				throw new ObjectDisposedException(this.GetType().FullName);
		}

		async Task CloseConnection()
		{
			if (this._connectionClosed)
				return;

			await this.Transport.StopAsync();
			this._connectionClosed = true;
		}
		public void Dispose()
		{
			if (this._disposed)
				return;

			this._ticksDisposable.Dispose();
			this.CloseConnection().Wait();
			this.ReleaseAllFrameHandlers(RSocketProtocol.ErrorCodes.Connection_Close, null);

			this.Dispose(true);

			this._disposed = true;
		}
		protected virtual void Dispose(bool disposing)
		{

		}
	}
}
