using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Reactive.Linq;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using IRSocketStream = System.IObserver<(System.Buffers.ReadOnlySequence<byte> metadata, System.Buffers.ReadOnlySequence<byte> data)>;

namespace RSocket
{
	public interface IRSocketChannel
	{
		Task Send((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value);
	}

	public partial class RSocket : IRSocketProtocol
	{
		public const int INITIALDEFAULT = int.MaxValue;
		RSocketOptions Options { get; set; }

		//TODO Hide.
		public IRSocketTransport Transport { get; set; }
		private int StreamId = 1 - 2;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
		private int NewStreamId() => Interlocked.Add(ref StreamId, 2);  //TODO SPEC: To reuse or not... Should tear down the client if this happens or have to skip in-use IDs.

		private ConcurrentDictionary<int, IRSocketStream> Dispatcher = new ConcurrentDictionary<int, IRSocketStream>();
		private int StreamDispatch(int id, IRSocketStream transform) { Dispatcher[id] = transform; return id; }
		private int StreamDispatch(IRSocketStream transform) => StreamDispatch(NewStreamId(), transform);
		//TODO Stream Destruction - i.e. removal from the dispatcher.

		protected IDisposable ChannelSubscription;      //TODO Tracking state for channels

		public RSocket(IRSocketTransport transport, RSocketOptions options = default)
		{
			Transport = transport;
			Options = options ?? RSocketOptions.Default;
		}

		//public RSocket(IRSocketServerTransport transport, RSocketOptions options = default)
		//{
		//	Transport = new ServerTransport(transport);
		//	Options = options ?? RSocketOptions.Default;
		//}

		//private struct ServerTransport : IRSocketTransport
		//{
		//	IRSocketServerTransport Transport;
		//	public ServerTransport(IRSocketServerTransport transport) { Transport = transport; }
		//	public PipeReader Input => Transport.Input;
		//	public PipeWriter Output => Transport.Output;
		//	public Task StartAsync(CancellationToken cancel = default) => Transport.StartAsync(cancel);
		//	public Task StopAsync() => Transport.StopAsync();
		//}

		/// <summary>Binds the RSocket to its Transport and begins handling messages.</summary>
		/// <param name="cancel">Cancellation for the handler. Requesting cancellation will stop message handling.</param>
		/// <returns>The handler task.</returns>
		public Task Connect(CancellationToken cancel = default) => RSocketProtocol.Handler(this, Transport.Input, cancel);

		//public async Task<RSocket> ConnectAsync()
		//{
		//	await Transport.StartAsync();
		//	var server = RSocketProtocol.Handler(this, Transport.Input, CancellationToken.None, name: nameof(RSocketClient));
		//	////TODO Move defaults to policy object
		//	new RSocketProtocol.Setup(keepalive: TimeSpan.FromSeconds(60), lifetime: TimeSpan.FromSeconds(180), metadataMimeType: "binary", dataMimeType: "binary").Write(Transport.Output);
		//	await Transport.Output.FlushAsync();
		//	return this;
		//}

		//TODO SPEC: A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.

		public IAsyncEnumerable<T> RequestChannel<TSource, T>(IAsyncEnumerable<TSource> source, Func<TSource, ReadOnlySequence<byte>> sourcemapper,
			Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<TSource, T>(stream => RequestChannel(stream, data, metadata), source, _ => (default, sourcemapper(_)), value => resultmapper(value));

		public async Task<IRSocketChannel> RequestChannel(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var id = StreamDispatch(stream);
			new RSocketProtocol.RequestChannel(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).Write(Transport.Output, data, metadata);
			await Transport.Output.FlushAsync();
			var channel = new ChannelHandler(this, id);
			return channel;
		}

		protected class ChannelHandler : IRSocketChannel       //TODO hmmm...
		{
			readonly RSocket Socket;
			readonly int Stream;

			public ChannelHandler(RSocket socket, int stream) { Socket = socket; Stream = stream; }

			public Task Send((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value)
			{
				if (!Socket.Dispatcher.ContainsKey(Stream)) { throw new InvalidOperationException("Channel is closed"); }
				new RSocketProtocol.Payload(Stream, value.data, value.metadata, next: true).Write(Socket.Transport.Output, value.data, value.metadata);
				var result = Socket.Transport.Output.FlushAsync();
				return result.IsCompleted ? Task.CompletedTask : result.AsTask();
			}
		}


		public IAsyncEnumerable<T> RequestStream<T>(Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<T>(stream => RequestStream(stream, data, metadata), value => resultmapper(value));

		public Task RequestStream(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			if (initial <= INITIALDEFAULT) { initial = Options.InitialRequestSize; }
			var id = StreamDispatch(stream);
			new RSocketProtocol.RequestStream(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).Write(Transport.Output, data, metadata);
			var result = Transport.Output.FlushAsync();
			return result.IsCompleted ? Task.CompletedTask : result.AsTask();
		}

		public Task<T> RequestResponse<T>(Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<T>(stream => RequestResponse(stream, data, metadata), resultmapper).ExecuteAsync();

		public Task RequestResponse(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
		{
			var id = StreamDispatch(stream);
			new RSocketProtocol.RequestResponse(id, data, metadata).Write(Transport.Output, data, metadata);
			var result = Transport.Output.FlushAsync();
			return result.IsCompleted ? Task.CompletedTask : result.AsTask();
		}


		public Task RequestFireAndForget(
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<bool>(stream => RequestFireAndForget(stream, data, metadata), _ => true).ExecuteAsync(result: true);

		public Task RequestFireAndForget(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
		{
			var id = StreamDispatch(stream);
			new RSocketProtocol.RequestFireAndForget(id, data, metadata).Write(Transport.Output, data, metadata);
			var result = Transport.Output.FlushAsync();
			return result.IsCompleted ? Task.CompletedTask : result.AsTask();
		}


		void IRSocketProtocol.Payload(in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			//Console.WriteLine($"{value.Header.Stream:0000}===>{Encoding.UTF8.GetString(value.Data.ToArray())}");
			if (Dispatcher.TryGetValue(message.Stream, out var transform))
			{
				//TODO FIXIE!
				if (message.IsNext) { transform.OnNext((metadata, data)); }
				if (message.IsComplete) { transform.OnCompleted(); }
			}
			else
			{
				//TODO Log missing stream here.
			}
		}


		//void IRSocketProtocol.Setup(in RSocketProtocol.Setup message) => Setup(message);
		public virtual void Setup(in RSocketProtocol.Setup value) => throw new InvalidOperationException($"Client cannot process Setup frames");    //TODO This exception just stalls processing. Need to make sure it's handled.
		void IRSocketProtocol.Error(in RSocketProtocol.Error message) { throw new NotImplementedException(); }  //TODO Handle Errors!
		void IRSocketProtocol.RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => throw new NotImplementedException();

		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), Task<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)>> Responder { get; set; } = request => throw new NotImplementedException();

		void IRSocketProtocol.RequestResponse(in RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Respond(message.Stream).Start();
			async Task Respond(int stream)
			{
				var (Data, Metadata) = await Responder((data, metadata));     //TODO Handle Errors.
				new RSocketProtocol.Payload(stream, Data, Metadata, next: true, complete: true).Write(Transport.Output, Data, Metadata);
				await Transport.Output.FlushAsync();
			}
		}


		public void Stream<TRequest, TResult>(
			Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TRequest> requestTransform,
			Func<TRequest, IAsyncEnumerable<TResult>> producer,
			Func<TResult, (ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> resultTransform) =>
			Streamer = (request) => from result in producer(requestTransform(request)) select resultTransform(result);

		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>> Streamer { get; set; } = request => throw new NotImplementedException();

		void IRSocketProtocol.RequestStream(in RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Stream(message.Stream).Start();
			async Task Stream(int stream)
			{
				var source = Streamer((data, metadata));     //TODO Handle Errors.
				await ForEach(source,
					action: value => new RSocketProtocol.Payload(stream, value.data, value.metadata, next: true).WriteFlush(Transport.Output, value.data, value.metadata),
					final: () => new RSocketProtocol.Payload(stream, complete: true).WriteFlush(Transport.Output));

				//var source = Streamer((data, metadata));     //TODO Handle Errors.
				//var enumerator = source.GetAsyncEnumerator();
				//try
				//{
				//	while (await enumerator.MoveNextAsync())
				//	{
				//		var (Data, Metadata) = enumerator.Current;
				//		new RSocketProtocol.Payload(stream, Data, Metadata, next: true).Write(Transport.Output, Data, Metadata);
				//		await Transport.Output.FlushAsync();
				//	}
				//	new RSocketProtocol.Payload(stream, complete: true).Write(Transport.Output);
				//	await Transport.Output.FlushAsync();
				//}
				//finally { await enumerator.DisposeAsync(); }
			}
		}


		//public void Channel<TSource, TResult>(Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata),
		//		(IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> Incoming,
		//		IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> Outgoing)>


		//	Func<TSource, (ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> outgoingMapper,
		//	Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), TResult> incomingMapper)
		//{
		//	var observable = Observable.Create<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>(observer => {
		//		Subscriber(observer).ConfigureAwait(false);
		//	});
		//	return observable
		//		.Select(value => Mapper((value.data, value.metadata)))
		//		.ToAsyncEnumerable()

		//		.GetAsyncEnumerator(cancellation);

		//	var receiver = new Receiver<TResult>(stream => Task.CompletedTask, incomingMapper);

		//	Channeler = request =>
		//	(
		//		receiver,
		//	);
		//}

		public void Channel<TRequest, TIncoming, TOutgoing>(Func<TRequest, IObservable<TIncoming>, IAsyncEnumerable<TOutgoing>> pipeline,
			Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TRequest> requestTransform,
			Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TIncoming> incomingTransform,
			Func<TOutgoing, (ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> outgoingTransform) =>
			Channeler = (request, incoming) => from result in pipeline(requestTransform(request), from item in incoming select incomingTransform(item)) select outgoingTransform(result);


		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>, IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>> Channeler { get; set; } = (request, incoming) => throw new NotImplementedException();


		void IRSocketProtocol.RequestChannel(in RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Channel(message.Stream).Start();
			async Task Channel(int stream, CancellationToken cancel = default)
			{
				//TODO Elsewhere in previous changes, bad Disposable.Empty
				var inc = Observable.Create<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>(observer => () => StreamDispatch(stream, observer));
				var outgoing = Channeler((data, metadata), inc);     //TODO Handle Errors.

				await ForEach(outgoing,
					action: value => new RSocketProtocol.Payload(stream, value.data, value.metadata, next: true).WriteFlush(Transport.Output, value.data, value.metadata),
					final: () => new RSocketProtocol.Payload(stream, complete: true).WriteFlush(Transport.Output));

				//var outgoingstream = ForEach(outgoing,
				//	action: value => new RSocketProtocol.Payload(stream, value.data, value.metadata, next: true).WriteFlush(Transport.Output, value.data, value.metadata),
				//	final: () => new RSocketProtocol.Payload(stream, complete: true).WriteFlush(Transport.Output));

				//var enumerator = outgoing.GetAsyncEnumerator();
				//try
				//{
				//	while (await enumerator.MoveNextAsync())
				//	{
				//		var (Data, Metadata) = enumerator.Current;
				//		new RSocketProtocol.Payload(stream, Data, Metadata, next: true).Write(Transport.Output, Data, Metadata);
				//		await Transport.Output.FlushAsync();
				//	}
				//	new RSocketProtocol.Payload(stream, complete: true).Write(Transport.Output);
				//	await Transport.Output.FlushAsync();
				//}
				//finally { await enumerator.DisposeAsync(); }
			}


			//	Channel(message.Stream).Start();

			//	//new Receiver<bool>()

			//	//new Receiver<bool>(stream => RequestFireAndForget(stream, data, metadata), _ => true).ExecuteAsync(result: true);
			//	//var id = StreamDispatch(stream);

			//	async Task Channel(int stream)
			//	{
			//		var (Incoming, Outoing) = Channeler((data, metadata));     //TODO Handle Errors.


			//		using (observable.Subscribe())
			//		{
			//			var enumerator = source.GetAsyncEnumerator();
			//			try
			//			{
			//				while (await enumerator.MoveNextAsync())
			//				{
			//					var (Data, Metadata) = enumerator.Current;
			//					new RSocketProtocol.Payload(stream, Data, Metadata, next: true).Write(Transport.Output, Data, Metadata);
			//					await Transport.Output.FlushAsync();
			//				}
			//				new RSocketProtocol.Payload(stream, complete: true).Write(Transport.Output);
			//				await Transport.Output.FlushAsync();
			//			}
			//			finally { await enumerator.DisposeAsync(); }
			//		}
			//	}
		}

		internal static async Task ForEach<TSource>(IAsyncEnumerable<TSource> source, Func<TSource, Task> action, CancellationToken cancel = default, Func<Task> final = default)
		{
			//No idea why this isn't public... https://github.com/dotnet/reactive/blob/master/Ix.NET/Source/System.Linq.Async/System/Linq/Operators/ForEach.cs#L58
			var enumerator = AsyncEnumerableExtensions.WithCancellation(source, cancel).ConfigureAwait(false).GetAsyncEnumerator();
			try
			{
				while (await enumerator.MoveNextAsync()) { await action(enumerator.Current); }
				await final?.Invoke();
			}
			finally { await enumerator.DisposeAsync(); }
		}
	}
}
