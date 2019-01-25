namespace RSocketRPCSample
{
	using System.Buffers;
	using System.Threading.Tasks;
	using RSocket;
	using RSocket.RPC;
	using Io.Rsocket.Rpc.Testing;

	[System.Runtime.CompilerServices.CompilerGenerated]
	interface ISimpleService
	{
		//String SERVICE = "io.rsocket.rpc.echo.EchoService";
		//String METHOD_FIRE_AND_FORGET = "fireAndForget";
		//String METHOD_REQUEST_RESPONSE = "requestResponse";
		//String METHOD_REQUEST_STREAM = "requestStream";
		//String METHOD_REQUEST_CHANNEL = "requestChannel";

		//reactor.core.publisher.Mono<Void> fireAndForget(com.google.protobuf.BytesValue message, io.netty.buffer.ByteBuf metadata);
		//reactor.core.publisher.Mono<com.google.protobuf.BytesValue> requestResponse(com.google.protobuf.BytesValue message, io.netty.buffer.ByteBuf metadata);
		//reactor.core.publisher.Flux<com.google.protobuf.BytesValue> requestStream(com.google.protobuf.BytesValue message, io.netty.buffer.ByteBuf metadata);
		//reactor.core.publisher.Flux<com.google.protobuf.BytesValue> requestChannel(org.reactivestreams.Publisher<com.google.protobuf.BytesValue> messages, io.netty.buffer.ByteBuf metadata);


		//rpc RequestReply(SimpleRequest) returns(SimpleResponse) { }
		//rpc FireAndForget(SimpleRequest) returns(google.protobuf.Empty) { }
		//rpc RequestStream(SimpleRequest) returns(stream SimpleResponse) { }
		//rpc StreamingRequestSingleResponse(stream SimpleRequest) returns(SimpleResponse) { }
		//rpc StreamingRequestAndResponse(stream SimpleRequest) returns(stream SimpleResponse) { }
	}

	[System.Runtime.CompilerServices.CompilerGenerated]
	public class SimpleService : RSocketService<SimpleService>, ISimpleService
	{
		private const string SERVICE = "io.rsocket.rpc.testing.protobuf" + "." + nameof(SimpleService);

		public SimpleService(RSocketClient client) : base(client) { }

		public Task<SimpleResponse> RequestReply(SimpleRequest message, ReadOnlySequence<byte> metadata = default) => __RequestResponse(message, Google.Protobuf.MessageExtensions.ToByteArray, SimpleResponse.Parser.ParseFrom, metadata, service: SERVICE);


		//rpc RequestReply(SimpleRequest) returns(SimpleResponse) { }
		//rpc FireAndForget(SimpleRequest) returns(google.protobuf.Empty) { }
		//rpc RequestStream(SimpleRequest) returns(stream SimpleResponse) { }
		//rpc StreamingRequestSingleResponse(stream SimpleRequest) returns(SimpleResponse) { }
		//rpc StreamingRequestAndResponse(stream SimpleRequest) returns(stream SimpleResponse) { }

		//public void fireAndForget(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestFireAndForget(null, data, metadata); }

		//public Task<SimpleResponse> RequestReply(SimpleRequest message, ReadOnlySequence<byte> metadata = default) => 
		//	base.__RequestResponse(ServicePrefix + nameof(SimpleService), nameof(RequestReply), message, source => Google.Protobuf.MessageExtensions.ToByteArray(source), result => SimpleResponse.Parser.ParseFrom(result), metadata);



		//public ReadOnlySequence<byte> requestResponse(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) => requestResponseAsync(data, metadata).Result;


		//public Task<ReadOnlySequence<byte>> requestStreamAsync(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) =>
		//	base.__RequestStream<ReadOnlySequence<byte>>(ServicePrefix + nameof(EchoService), nameof(requestStream), data, metadata);

		////Prefer this implementation.
		//public IObservable<ReadOnlySequence<byte>> requestStream(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) =>
		//	base.__RequestStream<ReadOnlySequence<byte>>(ServicePrefix + nameof(EchoService), nameof(requestStream), data, metadata);


		//Not real. Wrong signature.
		//public void requestChannel(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestChannel(null, data, metadata); }
	}
}
