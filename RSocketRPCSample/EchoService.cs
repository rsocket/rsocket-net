#pragma warning disable IDE1006 //Naming rule violation: These words must begin with upper case characters
namespace RSocketRPCSample
{
	using System.Buffers;
	using System.Threading.Tasks;
	using RSocket;
	using RSocket.RPC;

	[System.Runtime.CompilerServices.CompilerGenerated]
	interface IEchoService
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
	}

	[System.Runtime.CompilerServices.CompilerGenerated]
	public class EchoService : RSocketService, IEchoService
	{
		private const string SERVICE = "io.rsocket.rpc.echo" + "." + nameof(EchoService);

		public EchoService(RSocketClient client) : base(client) { }

		public Task fireAndForget(Google.Protobuf.WellKnownTypes.BytesValue message, ReadOnlySequence<byte> metadata = default) => __RequestFireAndForget(message, Google.Protobuf.MessageExtensions.ToByteArray, metadata, service: SERVICE);

		public Task<Google.Protobuf.WellKnownTypes.BytesValue> requestResponse(Google.Protobuf.WellKnownTypes.BytesValue message, ReadOnlySequence<byte> metadata = default) => __RequestResponse(message, Google.Protobuf.MessageExtensions.ToByteArray, Google.Protobuf.WellKnownTypes.BytesValue.Parser.ParseFrom, metadata, service: SERVICE);

		//public void fireAndForget(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestFireAndForget(null, data, metadata); }

		//public Task<ReadOnlySequence<byte>> requestResponse(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) =>
		//	base.__RequestResponse(ServicePrefix + nameof(EchoService), nameof(requestResponse), data, metadata);

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
