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

namespace RSocket
{
	public interface IRSocketChannel
	{
		Task Send(Payload value);
		Task Complete();
	}

	public partial class RSocket : IRSocketProtocol
	{
		internal PrefetchOptions Options { get; set; }

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
		public Task Connect(CancellationToken cancel = default) => RSocketProtocol.Handler(this, Transport.Input, cancel);
		public Task Setup(TimeSpan keepalive, TimeSpan lifetime, string metadataMimeType = null, string dataMimeType = null, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default) => new RSocketProtocol.Setup(keepalive, lifetime, metadataMimeType: metadataMimeType, dataMimeType: dataMimeType, data: data, metadata: metadata).WriteFlush(Transport.Output, data: data, metadata: metadata);


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
			Func<int, Task> channelEstablisher = async streamId =>
			{
				var channel = new RSocketProtocol.RequestStream(streamId, data, metadata, initialRequest: this.Options.GetInitialRequestSize(initial));
				await channel.WriteFlush(this.Transport.Output, data, metadata);
			};

			var incoming = new RequestStreamRequesterIncomingStream(this, channelEstablisher);
			return incoming;
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

		public virtual async Task RequestFireAndForget(ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		{
			var streamId = this.NewStreamId();
			await new RSocketProtocol.RequestFireAndForget(streamId, data, metadata).WriteFlush(this.Transport.Output, data, metadata);
		}

		public virtual void Setup(RSocketProtocol.Setup value) => throw new InvalidOperationException($"Client cannot process Setup frames");    //TODO This exception just stalls processing. Need to make sure it's handled.
		void IRSocketProtocol.Error(RSocketProtocol.Error message)
		{
			if (message.Stream > 0)
			{
				this.MessageDispatch(message.Stream, handler =>
				{
					handler.HandleError(message);
				});

				return;
			}

			throw new NotImplementedException();
		}  //TODO Handle Errors!

		void IRSocketProtocol.RequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.HandleRequestFireAndForget(message, metadata, data);
		}

		public virtual void HandleRequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			throw new NotImplementedException();
		}

		[Obsolete("This method has obsoleted.")]
		public void Respond<TRequest, TResult>(
			Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TRequest> requestTransform,
			Func<TRequest, IAsyncEnumerable<TResult>> producer,
			Func<TResult, Payload> resultTransform) =>
			Responder = (request) => (from result in producer(requestTransform(request)) select resultTransform(result)).FirstAsync();

		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), ValueTask<Payload>> Responder { get; set; } = request => throw new NotImplementedException();

		void IRSocketProtocol.RequestResponse(RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Schedule(message.Stream, async (stream, cancel) =>
			{
				var value = await Responder((data, metadata));     //TODO Handle Errors.
				await new RSocketProtocol.Payload(stream, value.Data, value.Metadata, next: true, complete: true).WriteFlush(Transport.Output, value.Data, value.Metadata);
			});
		}


		//[Obsolete("This method has obsoleted.")]
		//public void Stream<TRequest, TResult>(
		//	Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TRequest> requestTransform,
		//	Func<TRequest, IAsyncEnumerable<TResult>> producer,
		//	Func<TResult, (ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> resultTransform) =>
		//	Streamer = (request) => from result in producer(requestTransform(request)) select resultTransform(result);

		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<Payload>> Streamer { get; set; } = request => throw new NotImplementedException();

		void IRSocketProtocol.RequestStream(RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Schedule(message.Stream, async (stream, cancel) =>
			{
				Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<Payload>, IObservable<Payload>> channeler = (request, incoming) =>
				{
					var outgoing = this.Streamer((data, metadata));
					incoming.Subscribe(a => { });
					return outgoing;
				};

				RequestStreamResponderFrameHandler frameHandler = new RequestStreamResponderFrameHandler(this, stream, metadata, data, message.InitialRequest, channeler);

				await this.ExecuteFrameHandler(message.Stream, frameHandler);
			});
		}

		//TODO, probably need to have an IAE<T> pipeline overload too.

		//[Obsolete("This method has obsoleted.")]
		//public void Channel<TRequest, TIncoming, TOutgoing>(Func<TRequest, IObservable<TIncoming>, IAsyncEnumerable<TOutgoing>> pipeline,
		//	Func<PayloadContent, TRequest> requestTransform,
		//	Func<PayloadContent, TIncoming> incomingTransform,
		//	Func<TOutgoing, PayloadContent> outgoingTransform) =>
		//	Channeler1 = (request, incoming) => from result in pipeline(requestTransform(request), from item in incoming select incomingTransform(item)) select outgoingTransform(result);

		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IPublisher<Payload>, IObservable<Payload>> Channeler { get; set; } = (request, incoming) => throw new NotImplementedException();

		void IRSocketProtocol.RequestChannel(RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Schedule(message.Stream, async (stream, cancel) =>
			{
				ResponderFrameHandler frameHandler = new ResponderFrameHandler(this, stream, metadata, data, message.InitialRequest, this.Channeler);
				await this.ExecuteFrameHandler(message.Stream, frameHandler);
			});
		}

		void IRSocketProtocol.Payload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.MessageDispatch(message.Stream, handler =>
			{
				handler.HandlePayload(message, metadata, data);
			});
		}
		void IRSocketProtocol.RequestN(RSocketProtocol.RequestN message)
		{
			this.MessageDispatch(message.Stream, handler =>
			{
				handler.HandleRequestN(message);
			});
		}
		void IRSocketProtocol.Cancel(RSocketProtocol.Cancel message)
		{
			this.MessageDispatch(message.Stream, handler =>
			{
				handler.HandleCancel(message);
			});
		}

		void MessageDispatch(int streamId, Action<IFrameHandler> act)
		{
			if (this.FrameHandlerDispatcher.TryGetValue(streamId, out var frameHandler))
			{
				act(frameHandler);
			}
			else
			{
#if DEBUG
				Console.WriteLine($"missing handler: {streamId}");
#endif
				//TODO Log missing handler here.
			}
		}
		async Task ExecuteFrameHandler(int streamId, IFrameHandler frameHandler)
		{
			this.FrameHandlerDispatch(streamId, frameHandler);
			try
			{
				await frameHandler.ToTask();
			}
			finally
			{
				this.FrameHandlerRemove(streamId);
				frameHandler.Dispose();
#if DEBUG
				Console.WriteLine($"Responder.frameHandler.Dispose(): stream[{streamId}]");
#endif
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
	}
}
