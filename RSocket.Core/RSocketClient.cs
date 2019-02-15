using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketSerializer { ReadOnlySequence<byte> Serialize<T>(in T item); }         //FUTURE C#8.0 static interface member
	public interface IRSocketDeserializer { T Deserialize<T>(in ReadOnlySequence<byte> data); }     //FUTURE C#8.0 static interface member


	public class RSocketClient : RSocket
	{
		RSocketOptions Options { get; set; }

		public RSocketClient(IRSocketTransport transport, RSocketOptions options = default) : base(transport, options) { }

		public new async Task<RSocketClient> ConnectAsync() { await base.ConnectAsync(); return this; }		//TODO Not worth it for Unit Tests. Remove.

		//Ugh, these are all garbage. Remove in favor of the transformation ones.
		public Task<IRSocketChannel> RequestChannel<TData>(IRSocketStream stream, TData data, ReadOnlySpan<byte> metadata = default, int initial = INITIALDEFAULT) => RequestChannel(stream, RequestDataSerializer.Serialize(data), metadata, initial);
		public Task<IRSocketChannel> RequestChannel<TMetadata>(IRSocketStream stream, ReadOnlySpan<byte> data, TMetadata metadata = default, int initial = INITIALDEFAULT) => RequestChannel(stream, data, RequestMetadataSerializer.Serialize(metadata), initial);
		public Task<IRSocketChannel> RequestChannel<TData, TMetadata>(IRSocketStream stream, TData data, TMetadata metadata = default, int initial = INITIALDEFAULT) => RequestChannel(stream, RequestDataSerializer.Serialize(data), RequestMetadataSerializer.Serialize(metadata), initial);

		public Task RequestStream<TData>(IRSocketStream stream, TData data, ReadOnlySpan<byte> metadata = default, int initial = INITIALDEFAULT) => RequestStream(stream, RequestDataSerializer.Serialize(data), metadata, initial);
		public Task RequestStream<TMetadata>(IRSocketStream stream, ReadOnlySpan<byte> data, TMetadata metadata = default, int initial = INITIALDEFAULT) => RequestStream(stream, data, RequestMetadataSerializer.Serialize(metadata), initial);
		public Task RequestStream<TData, TMetadata>(IRSocketStream stream, TData data, TMetadata metadata = default, int initial = INITIALDEFAULT) => RequestStream(stream, RequestDataSerializer.Serialize(data), RequestMetadataSerializer.Serialize(metadata), initial);


		public Task RequestFireAndForget<TData>(IRSocketStream stream, TData data, ReadOnlySpan<byte> metadata = default) => RequestFireAndForget(stream, RequestDataSerializer.Serialize(data), metadata);
		public Task RequestFireAndForget<TMetadata>(IRSocketStream stream, ReadOnlySpan<byte> data, TMetadata metadata = default) => RequestFireAndForget(stream, data, RequestMetadataSerializer.Serialize(metadata));
		public Task RequestFireAndForget<TData, TMetadata>(IRSocketStream stream, TData data, TMetadata metadata = default) => RequestFireAndForget(stream, RequestDataSerializer.Serialize(data), RequestMetadataSerializer.Serialize(metadata));


		public Task RequestResponse<TData>(IRSocketStream stream, TData data, ReadOnlySpan<byte> metadata = default) => RequestFireAndForget(stream, RequestDataSerializer.Serialize(data), metadata);
		public Task RequestResponse<TMetadata>(IRSocketStream stream, ReadOnlySpan<byte> data, TMetadata metadata = default) => RequestResponse(stream, data, RequestMetadataSerializer.Serialize(metadata));
		public Task RequestResponse<TData, TMetadata>(IRSocketStream stream, TData data, TMetadata metadata = default) => RequestResponse(stream, RequestDataSerializer.Serialize(data), RequestMetadataSerializer.Serialize(metadata));


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
	}


}