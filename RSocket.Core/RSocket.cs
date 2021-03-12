using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using IRSocketStream = System.IObserver<RSocket.PayloadContent>;
using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketChannel
	{
		Task Send(PayloadContent value);
		Task Complete();
	}

	public partial class RSocket : IRSocketProtocol
	{
		internal PrefetchOptions Options { get; set; }

		//TODO Hide.
		public IRSocketTransport Transport { get; set; }
		private int StreamId = 1 - 2;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
		protected virtual int NewStreamId() => Interlocked.Add(ref StreamId, 2);  //TODO SPEC: To reuse or not... Should tear down the client if this happens or have to skip in-use IDs.

		private ConcurrentDictionary<int, IRSocketStream> Dispatcher = new ConcurrentDictionary<int, IRSocketStream>();
		private int StreamDispatch(int id, IRSocketStream transform) { Dispatcher[id] = transform; return id; }
		private int StreamDispatch(IRSocketStream transform) => StreamDispatch(NewStreamId(), transform);
		private IRSocketStream StreamRemove(int id)
		{
			this.Dispatcher.TryRemove(id, out var transform);
			return transform;
		}

		private ConcurrentDictionary<int, IObserver<int>> RequestNDispatcher = new ConcurrentDictionary<int, IObserver<int>>();
		private int RequestNDispatch(int id, IObserver<int> transform) { RequestNDispatcher[id] = transform; return id; }
		private IObserver<int> RequestNRemove(int id)
		{
			this.RequestNDispatcher.TryRemove(id, out var transform);
			return transform;
		}

		private ConcurrentDictionary<int, IFrameHandler> FrameHandlerDispatcher = new ConcurrentDictionary<int, IFrameHandler>();
		private int FrameHandlerDispatch(int id, IFrameHandler frameHandler) { FrameHandlerDispatcher[id] = frameHandler; return id; }
		private int FrameHandlerDispatch(IFrameHandler frameHandler) => FrameHandlerDispatch(NewStreamId(), frameHandler);
		private IFrameHandler FrameHandlerRemove(int id)
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
		public Task Connect(CancellationToken cancel = default) => RSocketProtocol.Handler(this, Transport.Input, cancel);
		public Task Setup(TimeSpan keepalive, TimeSpan lifetime, string metadataMimeType = null, string dataMimeType = null, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default) => new RSocketProtocol.Setup(keepalive, lifetime, metadataMimeType: metadataMimeType, dataMimeType: dataMimeType, data: data, metadata: metadata).WriteFlush(Transport.Output, data: data, metadata: metadata);


		//TODO SPEC: A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.

		public virtual IAsyncEnumerable<T> RequestChannel<TSource, T>(IAsyncEnumerable<TSource> source, Func<TSource, ReadOnlySequence<byte>> sourcemapper,
			Func<PayloadContent, T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<TSource, T>(stream => RequestChannel(stream, data, metadata), source, _ => new PayloadContent(default, sourcemapper(_)), value => resultmapper(value));

		public async Task<IRSocketChannel> RequestChannel(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var id = StreamDispatch(stream);
			await new RSocketProtocol.RequestChannel(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata);
			var channel = new ChannelHandler(this, id);
			return channel;
		}

		public async Task RequestChannel(ISubscriber<PayloadContent> subscriber, IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> source, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			/*
			 * A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.
			 */

			throw new NotImplementedException();
		}

		public IObservable<PayloadContent> RequestChannel(IObservable<PayloadContent> source, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			/*
			 * A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.
			 */

			var streamId = this.NewStreamId();

			var inc = Observable.Create<PayloadContent>(observer =>
			{
				IObserver<int> incomingMonitorObserver = null;
				IObservable<int> incomingMonitor = Observable.Create<int>(observer =>
				{
					incomingMonitorObserver = observer;
					return Disposable.Empty;
				});

				RequestChannelRequesterFrameHandler frameHandler = new RequestChannelRequesterFrameHandler(this, streamId, observer, metadata, data, source);
				var incomingMonitorTask = incomingMonitor.ToAsyncEnumerable().LastOrDefaultAsync().AsTask();
				this.FrameHandlerDispatch(streamId, frameHandler);

				new RSocketProtocol.RequestChannel(streamId, data, metadata, initialRequest: this.Options.GetInitialRequestSize(initial)).WriteFlush(this.Transport.Output, data, metadata).ConfigureAwait(false);

				Schedule(streamId, async (stream, cancel) =>
				{
					try
					{
						await Task.WhenAll(frameHandler.AsTask(), incomingMonitorTask);
					}
					finally
					{
						this.FrameHandlerRemove(streamId);
						frameHandler.Dispose();
						Console.WriteLine("RequestChannel.client.frameHandler.Dispose()");
					}
				});

				return () =>
				{
					incomingMonitorObserver.OnCompleted();
				};
			});

			var incoming = new IncomingStreamWrapper<PayloadContent>(inc, this, streamId);

			return incoming;
		}

		protected internal class ChannelHandler : IRSocketChannel       //TODO hmmm...
		{
			readonly RSocket Socket;
			readonly int Stream;

			public ChannelHandler(RSocket socket, int stream) { Socket = socket; Stream = stream; }

			public Task Send(PayloadContent value)
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


		public virtual IAsyncEnumerable<T> RequestStream<T>(Func<PayloadContent, T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<T>(stream => RequestStream(stream, data, metadata), value => resultmapper(value));

		public Task RequestStream(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var id = StreamDispatch(stream);
			return new RSocketProtocol.RequestStream(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata);
		}

		public async Task RequestStream(ISubscriber<PayloadContent> subscriber, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var streamId = this.NewStreamId();

			RequestStreamRequesterFrameHandler frameHandler = new RequestStreamRequesterFrameHandler(this, streamId, subscriber, metadata, data, initial);

			this.FrameHandlerDispatch(streamId, frameHandler);
			try
			{
				await frameHandler.AsTask();
			}
			finally
			{
				this.FrameHandlerRemove(streamId);
				frameHandler.Dispose();
				Console.WriteLine("RequestStream.client.frameHandler.Dispose()");
			}
		}

		public virtual Task<T> RequestResponse<T>(Func<PayloadContent, T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<T>(stream => RequestResponse(stream, data, metadata), resultmapper).ExecuteAsync();

		public Task RequestResponse(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
		{
			var id = StreamDispatch(stream);
			return new RSocketProtocol.RequestResponse(id, data, metadata).WriteFlush(Transport.Output, data, metadata);
		}


		public virtual Task RequestFireAndForget(
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<bool>(stream => RequestFireAndForget(stream, data, metadata), _ => true).ExecuteAsync(result: true);

		public Task RequestFireAndForget(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
		{
			var id = StreamDispatch(stream);
			return new RSocketProtocol.RequestFireAndForget(id, data, metadata).WriteFlush(Transport.Output, data, metadata);
		}


		void IRSocketProtocol.Payload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.HandlerMessage(message.Stream, handler =>
			{
				handler.HandlePayload(message, metadata, data);
			});
		}


		void Schedule(int stream, Func<int, CancellationToken, Task> operation, CancellationToken cancel = default)
		{
			var task = operation(stream, cancel);
			if (!task.IsCompleted)
			{
				task.ConfigureAwait(false); //FUTURE Someday might want to schedule these in a different pool or perhaps track all in-flight tasks.
			}
		}


		public virtual void Setup(RSocketProtocol.Setup value) => throw new InvalidOperationException($"Client cannot process Setup frames");    //TODO This exception just stalls processing. Need to make sure it's handled.
		void IRSocketProtocol.Error(RSocketProtocol.Error message) { throw new NotImplementedException(); }  //TODO Handle Errors!
																											 //void IRSocketProtocol.RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => throw new NotImplementedException();
		void IRSocketProtocol.RequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.HandleRequestFireAndForget(message, metadata, data);
		}

		public virtual void HandleRequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			throw new NotImplementedException();
		}

		public void Respond<TRequest, TResult>(
			Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TRequest> requestTransform,
			Func<TRequest, IAsyncEnumerable<TResult>> producer,
			Func<TResult, (ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> resultTransform) =>
			Responder = (request) => (from result in producer(requestTransform(request)) select resultTransform(result)).FirstAsync();

		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), ValueTask<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)>> Responder { get; set; } = request => throw new NotImplementedException();

		void IRSocketProtocol.RequestResponse(RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Schedule(message.Stream, async (stream, cancel) =>
			{
				var value = await Responder((data, metadata));     //TODO Handle Errors.
				await new RSocketProtocol.Payload(stream, value.Data, value.Metadata, next: true, complete: true).WriteFlush(Transport.Output, value.Data, value.Metadata);
			});
		}


		public void Stream<TRequest, TResult>(
			Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TRequest> requestTransform,
			Func<TRequest, IAsyncEnumerable<TResult>> producer,
			Func<TResult, (ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> resultTransform) =>
			Streamer = (request) => from result in producer(requestTransform(request)) select resultTransform(result);

		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IAsyncEnumerable<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)>> Streamer { get; set; } = request => throw new NotImplementedException();

		void IRSocketProtocol.RequestStream(RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			int initialRequest = message.InitialRequest;

			Schedule(message.Stream, async (stream, cancel) =>
			{
				RequestStreamResponderFrameHandler frameHandler = new RequestStreamResponderFrameHandler(this, message, metadata, data);

				this.FrameHandlerDispatch(message.Stream, frameHandler);
				try
				{
					await frameHandler.AsTask();
				}
				finally
				{
					this.FrameHandlerRemove(message.Stream);
					frameHandler.Dispose();
					Console.WriteLine("RequestStream.server.frameHandler.Dispose()");
				}
				return;


			});
		}

		//TODO, probably need to have an IAE<T> pipeline overload too.

		public void Channel<TRequest, TIncoming, TOutgoing>(Func<TRequest, IObservable<TIncoming>, IAsyncEnumerable<TOutgoing>> pipeline,
			Func<PayloadContent, TRequest> requestTransform,
			Func<PayloadContent, TIncoming> incomingTransform,
			Func<TOutgoing, PayloadContent> outgoingTransform) =>
			Channeler = (request, incoming, subscription) => from result in pipeline(requestTransform(request), from item in incoming select incomingTransform(item)) select outgoingTransform(result);

		public Func<PayloadContent, IObservable<PayloadContent>, ISubscription, IAsyncEnumerable<PayloadContent>> Channeler { get; set; } = (request, incoming, subscription) => throw new NotImplementedException();

		public Func<PayloadContent, IObservable<PayloadContent>, IObservable<PayloadContent>> Channeler1 { get; set; } = (request, incoming) => throw new NotImplementedException();

		void IRSocketProtocol.RequestChannel(RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Schedule(message.Stream, async (stream, cancel) =>
			{
				RequestChannelResponderFrameHandler frameHandler = new RequestChannelResponderFrameHandler(this, message, metadata, data);

				this.FrameHandlerDispatch(message.Stream, frameHandler);
				try
				{
					await frameHandler.AsTask();
				}
				finally
				{
					this.FrameHandlerRemove(message.Stream);
					frameHandler.Dispose();
					Console.WriteLine("RequestChannel.server.frameHandler.Dispose()");
				}
			});
		}


		public void RequestN(RSocketProtocol.RequestN message)
		{
			this.HandlerMessage(message.Stream, handler =>
			{
				handler.HandleRequestN(message);
			});
		}
		public void Cancel(RSocketProtocol.Cancel message)
		{
			this.HandlerMessage(message.Stream, handler =>
			{
				handler.HandleCancel(message);
			});
		}

		void HandlerMessage(int streamId, Action<IFrameHandler> act)
		{
			if (this.FrameHandlerDispatcher.TryGetValue(streamId, out var frameHandler))
			{
				act(frameHandler);
			}
			else
			{
				Console.WriteLine("TODO Log missing handler here");
				//TODO Log missing handler here.
			}
		}

		static async Task ForEach<TSource>(IAsyncEnumerable<TSource> source, Func<TSource, Task> action, CancellationToken cancel = default, Func<Task> final = default)
		{
			await source.ForEachAsync(item =>
			{
				action(item);
			}, cancel);

			if (!cancel.IsCancellationRequested)
				await final?.Invoke();
		}
	}
}
