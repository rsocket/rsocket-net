using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketSerializer { ReadOnlySequence<byte> Serialize<T>(in T item); }			//FUTURE C#8.0 static interface member
	public interface IRSocketDeserializer { T Deserialize<T>(in ReadOnlySequence<byte> data); }     //FUTURE C#8.0 static interface member


	public class RSocketClient : IRSocketProtocol
	{
		public const int INITIALDEFAULT = -1;

		IRSocketTransport Transport { get; set; }
		RSocketClientOptions Options { get; set; }
		private int StreamId = 1 - 2;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
		private int NewStreamId() => Interlocked.Add(ref StreamId, 2);  //TODO SPEC: To reuse or not... Should tear down the client if this happens or have to skip in-use IDs.

		private ConcurrentDictionary<int, IRSocketStream> Dispatcher = new ConcurrentDictionary<int, IRSocketStream>();
		private int StreamDispatch(IRSocketStream transform) { var id = NewStreamId(); Dispatcher[id] = transform; return id; }
		//TODO Stream Destruction - i.e. removal from the dispatcher.

		protected IDisposable ChannelSubscription;      //TODO Tracking state for channels

		public RSocketClient(IRSocketTransport transport, RSocketClientOptions options = default)
		{
			Transport = transport;
			Options = options ?? RSocketClientOptions.Default;
			//TODO Fluent options method here with copied default. Oh, and the default should probably be immutable.
		}

		public async Task<RSocketClient> ConnectAsync()
		{
			await Transport.ConnectAsync();
			var server = RSocketProtocol.Handler2(this, Transport.Input, CancellationToken.None);
			////TODO Move defaults to policy object
			new RSocketProtocol.Setup(keepalive: TimeSpan.FromSeconds(60), lifetime: TimeSpan.FromSeconds(180), metadataMimeType: "binary", dataMimeType: "binary").Write(Transport.Output);
			await Transport.Output.FlushAsync();
			return this;
		}

		//TODO SPEC: A requester MUST not send PAYLOAD frames after the REQUEST_CHANNEL frame until the responder sends a REQUEST_N frame granting credits for number of PAYLOADs able to be sent.

		public Task RequestChannel<TData>(IRSocketStream stream, TData data, ReadOnlySpan<byte> metadata = default, int initial = INITIALDEFAULT) => RequestChannel(stream, RequestDataSerializer.Serialize(data), metadata, initial);
		public Task RequestChannel<TMetadata>(IRSocketStream stream, ReadOnlySpan<byte> data, TMetadata metadata = default, int initial = INITIALDEFAULT) => RequestChannel(stream, data, RequestMetadataSerializer.Serialize(metadata), initial);
		public Task RequestChannel<TData, TMetadata>(IRSocketStream stream, TData data, TMetadata metadata = default, int initial = INITIALDEFAULT) => RequestChannel(stream, RequestDataSerializer.Serialize(data), RequestMetadataSerializer.Serialize(metadata), initial);
		public Task RequestChannel(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = INITIALDEFAULT)
		{
			if (initial <= INITIALDEFAULT) { initial = Options.InitialRequestSize; }
			var id = StreamDispatch(stream);        //TODO This needs to be returned to the caller to allow them to send more messages.
			new RSocketProtocol.RequestChannel(id, data, metadata, initialRequest: initial).Write(Transport.Output, data, metadata);
			var result = Transport.Output.FlushAsync();
			return result.IsCompleted ? Task.CompletedTask : result.AsTask();
		}

		//TODO As Extensions?
		public Task RequestStream<TData>(IRSocketStream stream, TData data, ReadOnlySpan<byte> metadata = default, int initial = INITIALDEFAULT) => RequestStream(stream, RequestDataSerializer.Serialize(data), metadata, initial);
		public Task RequestStream<TMetadata>(IRSocketStream stream, ReadOnlySpan<byte> data, TMetadata metadata = default, int initial = INITIALDEFAULT) => RequestStream(stream, data, RequestMetadataSerializer.Serialize(metadata), initial);
		public Task RequestStream<TData, TMetadata>(IRSocketStream stream, TData data, TMetadata metadata = default, int initial = INITIALDEFAULT) => RequestStream(stream, RequestDataSerializer.Serialize(data), RequestMetadataSerializer.Serialize(metadata), initial);
		public Task RequestStream(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, int initial = INITIALDEFAULT)
		{
			if (initial <= INITIALDEFAULT) { initial = Options.InitialRequestSize; }
			var id = StreamDispatch(stream);
			new RSocketProtocol.RequestStream(id, data, metadata, initialRequest: initial).Write(Transport.Output, data, metadata);
			var result = Transport.Output.FlushAsync();
			return result.IsCompleted ? Task.CompletedTask : result.AsTask();
		}

		public Task RequestFireAndForget<TData>(IRSocketStream stream, TData data, ReadOnlySpan<byte> metadata = default) => RequestFireAndForget(stream, RequestDataSerializer.Serialize(data), metadata);
		public Task RequestFireAndForget<TMetadata>(IRSocketStream stream, ReadOnlySpan<byte> data, TMetadata metadata = default) => RequestFireAndForget(stream, data, RequestMetadataSerializer.Serialize(metadata));
		public Task RequestFireAndForget<TData, TMetadata>(IRSocketStream stream, TData data, TMetadata metadata = default) => RequestFireAndForget(stream, RequestDataSerializer.Serialize(data), RequestMetadataSerializer.Serialize(metadata));
		public Task RequestFireAndForget(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
		{
			var id = StreamDispatch(stream);
			new RSocketProtocol.RequestFireAndForget(id, data, metadata).Write(Transport.Output, data, metadata);
			var result = Transport.Output.FlushAsync();
			return result.IsCompleted ? Task.CompletedTask : result.AsTask();
		}

		public Task RequestResponse<TData>(IRSocketStream stream, TData data, ReadOnlySpan<byte> metadata = default) => RequestFireAndForget(stream, RequestDataSerializer.Serialize(data), metadata);
		public Task RequestResponse<TMetadata>(IRSocketStream stream, ReadOnlySpan<byte> data, TMetadata metadata = default) => RequestResponse(stream, data, RequestMetadataSerializer.Serialize(metadata));
		public Task RequestResponse<TData, TMetadata>(IRSocketStream stream, TData data, TMetadata metadata = default) => RequestResponse(stream, RequestDataSerializer.Serialize(data), RequestMetadataSerializer.Serialize(metadata));
		public Task RequestResponse(IRSocketStream stream, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
		{
			var id = StreamDispatch(stream);
			new RSocketProtocol.RequestResponse(id, data, metadata).Write(Transport.Output, data, metadata);
			var result = Transport.Output.FlushAsync();
			return result.IsCompleted ? Task.CompletedTask : result.AsTask();
		}


		//public ValueTask<System.IO.Pipelines.FlushResult> RequestChannel(IRSocketStream stream, ReadOnlySpan<byte> data, ReadOnlySpan<byte> metadata = default, int initial = INITIALDEFAULT)
		//{
		//	if (initial < 0) { initial = Options.InitialRequestSize; }
		//	var id = StreamDispatch(stream);
		//	new RSocketProtocol.RequestChannel(id, data, initialRequest: initial).Write(Transport.Output);
		//	return Transport.Output.FlushAsync();
		//}

		public IRSocketSerializer RequestDataSerializer = Defaults.Request;
		public IRSocketSerializer RequestMetadataSerializer = Defaults.Request;
		public IRSocketDeserializer ResponseDataDeserializer = Defaults.Response;
		public IRSocketDeserializer ResponseMetadataDeserializer = Defaults.Response;

		private sealed class Defaults : IRSocketSerializer, IRSocketDeserializer
		{
			private bool isRequest;
			static public readonly Defaults Request = new Defaults() { isRequest = true };
			static public readonly Defaults Response = new Defaults() { isRequest = false };
			ReadOnlySequence<byte> IRSocketSerializer.Serialize<T>(in T item) => throw new NotSupportedException(isRequest ? "The RSocket client was not provided with a request serializer." : "The RSocket client was not provided with a response serializer.");
			T IRSocketDeserializer.Deserialize<T>(in ReadOnlySequence<byte> data) => throw new NotSupportedException(isRequest ? "The RSocket client was not provided with a request deserializer." : "The RSocket client was not provided with a response deserializer.");
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
		void IRSocketProtocol.RequestStream(in RSocketProtocol.RequestStream message) => throw new NotImplementedException(); //TODO How to handle unexpected messagess...
		void IRSocketProtocol.Error(in RSocketProtocol.Error message) { throw new NotImplementedException(); }	//TODO Handle Errors!
	}
}
