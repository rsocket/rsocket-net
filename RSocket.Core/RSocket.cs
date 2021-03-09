using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using IRSocketStream = System.IObserver<(System.Buffers.ReadOnlySequence<byte> metadata, System.Buffers.ReadOnlySequence<byte> data)>;
using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketChannel
	{
		Task Send((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value);
		Task Complete();
	}

	public partial class RSocket : IRSocketProtocol
	{
		PrefetchOptions Options { get; set; }

		//TODO Hide.
		public IRSocketTransport Transport { get; set; }
		private int StreamId = 1 - 2;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
		private int NewStreamId() => Interlocked.Add(ref StreamId, 2);  //TODO SPEC: To reuse or not... Should tear down the client if this happens or have to skip in-use IDs.

		private ConcurrentDictionary<int, IRSocketStream> Dispatcher = new ConcurrentDictionary<int, IRSocketStream>();
		private int StreamDispatch(int id, IRSocketStream transform) { Dispatcher[id] = transform; return id; }
		private int StreamDispatch(IRSocketStream transform) => StreamDispatch(NewStreamId(), transform);

		private ConcurrentDictionary<int, IObserver<int>> RequestNDispatcher = new ConcurrentDictionary<int, IObserver<int>>();
		private int RequestNDispatch(int id, IObserver<int> transform) { RequestNDispatcher[id] = transform; return id; }

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
			Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<TSource, T>(stream => RequestChannel(stream, data, metadata), source, _ => (default, sourcemapper(_)), value => resultmapper(value));

		public async Task<IRSocketChannel> RequestChannel(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var id = StreamDispatch(stream);
			await new RSocketProtocol.RequestChannel(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata);
			var channel = new ChannelHandler(this, id);
			return channel;
		}

		public async Task RequestChannel(ISubscriber<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> subscriber, IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> source, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			/*
			 * A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.
			 */

			var streamId = this.NewStreamId();

			var observable = Observable.Create<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>(observer =>
			{
				StreamReceiver receiver = new StreamReceiver(subscriber, observer);
				StreamDispatch(streamId, receiver);
				Subscription subscription = new Subscription(streamId, Transport.Output);
				subscriber.OnSubscribe(subscription);

				new RSocketProtocol.RequestChannel(streamId, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata).ConfigureAwait(false);
				return Disposable.Empty;
			});

			IObserver<int> requestNObserver = null;
			var requestNObservable = Observable.Create<int>(observer =>
			{
				requestNObserver = observer;
				RequestNDispatch(streamId, observer);
				return Disposable.Empty;
			});

			var payloadSource = MakeControllablePayloads(requestNObservable, source);
			var channel = new ChannelHandler(this, streamId);
			var sourceTask = ForEach(payloadSource,
				  action: async value =>
				  {
					  await channel.Send((value.Metadata, value.Data));
				  },
				  final: async () =>
				  {
					  await channel.Complete();
					  requestNObserver.OnCompleted();
				  });

			await Task.WhenAll(sourceTask, observable.ToAsyncEnumerable().LastOrDefaultAsync().AsTask());
		}

		protected class ChannelHandler : IRSocketChannel       //TODO hmmm...
		{
			readonly RSocket Socket;
			readonly int Stream;

			public ChannelHandler(RSocket socket, int stream) { Socket = socket; Stream = stream; }

			public Task Send((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value)
			{
				if (!Socket.Dispatcher.ContainsKey(Stream)) { throw new InvalidOperationException("Channel is closed"); }
				return new RSocketProtocol.Payload(Stream, value.data, value.metadata, next: true).WriteFlush(Socket.Transport.Output, value.data, value.metadata);
			}

			public Task Complete()
			{
				if (!Socket.Dispatcher.ContainsKey(Stream)) { throw new InvalidOperationException("Channel is closed"); }
				return new RSocketProtocol.Payload(Stream, complete: true).WriteFlush(Socket.Transport.Output);
			}
		}


		public virtual IAsyncEnumerable<T> RequestStream<T>(Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), T> resultmapper,
			ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			=> new Receiver<T>(stream => RequestStream(stream, data, metadata), value => resultmapper(value));

		public Task RequestStream(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var id = StreamDispatch(stream);
			return new RSocketProtocol.RequestStream(id, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata);
		}

		public async Task RequestStream(ISubscriber<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> subscriber, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = RSocketOptions.INITIALDEFAULT)
		{
			var observable = Observable.Create<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>(observer =>
			{
				StreamReceiver receiver = new StreamReceiver(subscriber, observer);
				var streamId = StreamDispatch(receiver);
				Subscription subscription = new Subscription(streamId, Transport.Output);
				subscriber.OnSubscribe(subscription);
				new RSocketProtocol.RequestStream(streamId, data, metadata, initialRequest: Options.GetInitialRequestSize(initial)).WriteFlush(Transport.Output, data, metadata).ConfigureAwait(false);

				return Disposable.Empty;
			});

			await observable.LastOrDefaultAsync();
		}

		public virtual Task<T> RequestResponse<T>(Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), T> resultmapper,
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


		void IRSocketProtocol.Payload(in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			//Console.WriteLine($"{value.Header.Stream:0000}===>{Encoding.UTF8.GetString(value.Data.ToArray())}");
			if (Dispatcher.TryGetValue(message.Stream, out var transform))
			{
				if (message.IsNext) { transform.OnNext((metadata, data)); }
				if (message.IsComplete) { transform.OnCompleted(); }
			}
			else
			{
				//TODO Log missing stream here.
			}
		}


		void Schedule(int stream, Func<int, CancellationToken, Task> operation, CancellationToken cancel = default)
		{
			var task = operation(stream, cancel);
			if (!task.IsCompleted) { task.ConfigureAwait(false); }         //FUTURE Someday might want to schedule these in a different pool or perhaps track all in-flight tasks.
		}


		public virtual void Setup(in RSocketProtocol.Setup value) => throw new InvalidOperationException($"Client cannot process Setup frames");    //TODO This exception just stalls processing. Need to make sure it's handled.
		void IRSocketProtocol.Error(in RSocketProtocol.Error message) { throw new NotImplementedException(); }  //TODO Handle Errors!
																												//void IRSocketProtocol.RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => throw new NotImplementedException();
		void IRSocketProtocol.RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
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

		void IRSocketProtocol.RequestResponse(in RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
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

		void IRSocketProtocol.RequestStream(in RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			int initialRequest = message.InitialRequest;

			Schedule(message.Stream, async (stream, cancel) =>
			{
				IObserver<int> requestNObserver = null;
				var requestNObservable = Observable.Create<int>(observer =>
				{
					requestNObserver = observer;
					RequestNDispatch(stream, observer);
					observer.OnNext(initialRequest);
					return Disposable.Empty;
				});

				var source = Streamer((data, metadata));     //TODO Handle Errors.
				source = MakeControllablePayloads(requestNObservable, source);

				await ForEach(source,
					action: async value =>
					 {
						 await new RSocketProtocol.Payload(stream, value.Data, value.Metadata, next: true).WriteFlush(Transport.Output, value.Data, value.Metadata);
					 },
					final: async () =>
					 {
						 await new RSocketProtocol.Payload(stream, complete: true).WriteFlush(Transport.Output);
						 requestNObserver.OnCompleted();
					 });
			});
		}

		//TODO, probably need to have an IAE<T> pipeline overload too.

		public void Channel<TRequest, TIncoming, TOutgoing>(Func<TRequest, IObservable<TIncoming>, IAsyncEnumerable<TOutgoing>> pipeline,
			Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TRequest> requestTransform,
			Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), TIncoming> incomingTransform,
			Func<TOutgoing, (ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> outgoingTransform) =>
			Channeler = (request, incoming, subscription) => from result in pipeline(requestTransform(request), from item in incoming select incomingTransform(item)) select outgoingTransform(result);

		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>, ISubscription, IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>> Channeler { get; set; } = (request, incoming, subscription) => throw new NotImplementedException();


		void IRSocketProtocol.RequestChannel(in RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			int initialRequest = message.InitialRequest;

			Schedule(message.Stream, async (stream, cancel) =>
			{
				var inc = Observable.Create<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>(observer =>
				{
					StreamDispatch(stream, observer);
					return Disposable.Empty;
				});

				IObserver<int> requestNObserver = null;
				var requestNObservable = Observable.Create<int>(observer =>
				{
					requestNObserver = observer;
					RequestNDispatch(stream, observer);
					observer.OnNext(initialRequest);
					return Disposable.Empty;
				});

				Subscription subscription = new Subscription(stream, this.Transport.Output);

				var outgoing = Channeler((data, metadata), inc, subscription);     //TODO Handle Errors.
				outgoing = MakeControllablePayloads(requestNObservable, outgoing);

				await ForEach(outgoing,
					action: async value =>
					 {
						 await new RSocketProtocol.Payload(stream, value.data, value.metadata, next: true).WriteFlush(Transport.Output, value.data, value.metadata);
					 },
					final: async () =>
					{
						await new RSocketProtocol.Payload(stream, complete: true).WriteFlush(Transport.Output);
						requestNObserver.OnCompleted();
					});
			});
		}

		public void RequestN(in RSocketProtocol.RequestN message)
		{
			if (RequestNDispatcher.TryGetValue(message.Stream, out var transform))
			{
				transform.OnNext(message.RequestNumber);
			}
			else
			{
				//TODO Log missing stream here.
			}
		}

		static async Task ForEach<TSource>(IAsyncEnumerable<TSource> source, Func<TSource, Task> action, CancellationToken cancel = default, Func<Task> final = default)
		{
			await source.ForEachAsync(item =>
			{
				action(item);
			}, cancel);

			await final?.Invoke();
		}
	}
}
