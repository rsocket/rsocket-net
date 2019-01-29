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
	public class SimpleService : RSocketService, ISimpleService
	{
		private const string SERVICE = "io.rsocket.rpc.testing.protobuf" + "." + nameof(SimpleService);

		public SimpleService(RSocketClient client) : base(client) { }

		public Task<SimpleResponse> RequestReply(SimpleRequest message, ReadOnlySequence<byte> metadata = default) => __RequestResponse(message, Google.Protobuf.MessageExtensions.ToByteArray, SimpleResponse.Parser.ParseFrom, metadata, service: SERVICE);


		//rpc RequestReply(SimpleRequest) returns(SimpleResponse) { }
		//rpc FireAndForget(SimpleRequest) returns(google.protobuf.Empty) { }
		//rpc RequestStream(SimpleRequest) returns(stream SimpleResponse) { }
		//rpc StreamingRequestSingleResponse(stream SimpleRequest) returns(SimpleResponse) { }
		//rpc StreamingRequestAndResponse(stream SimpleRequest) returns(stream SimpleResponse) { }
	}
}
