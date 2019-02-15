using RSocket.Collections.Generic;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

		IRSocketTransport Transport { get; set; }
		//RSocketClientOptions Options { get; set; }
		private int StreamId = 1 - 2;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
		private int NewStreamId() => Interlocked.Add(ref StreamId, 2);  //TODO SPEC: To reuse or not... Should tear down the client if this happens or have to skip in-use IDs.

		private ConcurrentDictionary<int, IRSocketStream> Dispatcher = new ConcurrentDictionary<int, IRSocketStream>();
		private int StreamDispatch(IRSocketStream transform) { var id = NewStreamId(); Dispatcher[id] = transform; return id; }
		//TODO Stream Destruction - i.e. removal from the dispatcher.

		//TODO This isn't going to be used. Remove
		private ConcurrentDictionary<int, IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>> Dispatcher2 = new ConcurrentDictionary<int, IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>>();
		private int StreamDispatch(IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)> transform) { var id = NewStreamId(); Dispatcher2[id] = transform; return id; }

		//private ConcurrentDictionary<int, IRSocketChannel> Channels = new ConcurrentDictionary<int, IRSocketChannel>();

		protected IDisposable ChannelSubscription;      //TODO Tracking state for channels

		public RSocket(IRSocketTransport transport, RSocketOptions options = default)
		{
			Transport = transport;
			Options = options ?? RSocketOptions.Default;
		}


		public async Task<RSocket> ConnectAsync()
		{
			await Transport.ConnectAsync();
			var server = RSocketProtocol.Handler(this, Transport.Input, CancellationToken.None, name: nameof(RSocketClient));
			////TODO Move defaults to policy object
			new RSocketProtocol.Setup(keepalive: TimeSpan.FromSeconds(60), lifetime: TimeSpan.FromSeconds(180), metadataMimeType: "binary", dataMimeType: "binary").Write(Transport.Output);
			await Transport.Output.FlushAsync();
			return this;
		}

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
			var channel = new Channel(this, id);
			return channel;
		}

		class Channel : IRSocketChannel
		{
			readonly RSocket Socket;
			readonly int Stream;

			public Channel(RSocket socket, int stream) { Socket = socket; Stream = stream; }

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

		void IRSocketProtocol.Setup(in RSocketProtocol.Setup value) => throw new InvalidOperationException($"Client cannot process Setup frames");
		void IRSocketProtocol.Error(in RSocketProtocol.Error message) { throw new NotImplementedException(); }  //TODO Handle Errors!
		void IRSocketProtocol.RequestStream(in RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => throw new NotImplementedException(); //TODO How to handle unexpected messagess...
		void IRSocketProtocol.RequestResponse(in RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => throw new NotImplementedException();
		void IRSocketProtocol.RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => throw new NotImplementedException();
		void IRSocketProtocol.RequestChannel(in RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => throw new NotImplementedException();
	}
}
