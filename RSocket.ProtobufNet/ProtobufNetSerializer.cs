using System;
using System.Buffers;
using System.IO;

namespace RSocket.Serializers
{
	static public class ProtobufNetSerializerExtension
	{
		static public TRSocketClient UsingProtobufNetSerialization<TRSocketClient>(this TRSocketClient client, bool useData = true, bool useMetadata = true) where TRSocketClient : RSocketClient
		{
			var serializer = new ProtobufNetSerializer();
			if (useData) { client.RequestDataSerializer = serializer;  client.ResponseDataDeserializer = serializer; }
			if (useMetadata) { client.RequestMetadataSerializer = serializer; client.ResponseMetadataDeserializer = serializer; }
			return client;
		}
	}


	public sealed class ProtobufNetSerializer : IRSocketSerializer, IRSocketDeserializer
	{
		public ReadOnlySequence<byte> Serialize<T>(in T item)
		{
			if (item == default) { return default; }
			using (var stream = new MemoryStream())     //According to source, access to the buffer is safe after disposal (See. Dispose()): https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/IO/MemoryStream.cs#L133
			{
				ProtoBuf.Serializer.Serialize(stream, item);    //TODO Probably need a Stream -> IBufferWriter. I think the .NET folks have made one...
				if (stream.TryGetBuffer(out var buffer))
				{
					var seq = new ReadOnlySequence<byte>(buffer.Array, buffer.Offset, buffer.Count);

					return new ReadOnlySequence<byte>(stream.ToArray());    //TODO not great, but the stream is disposing, so we'd lose the buffer. 
				}
				else { throw new InvalidOperationException("Unable to get MemoryStream buffer"); }
			}
		}

		public T Deserialize<T>(in ReadOnlySequence<byte> data)
		{
			using (var stream = new MemoryStream(data.ToArray(), false))        //TODO A Span<byte> backed stream would be better here.
			{
				try { return ProtoBuf.Serializer.Deserialize<T>(stream); }
				catch (ProtoBuf.ProtoException ex) { System.Diagnostics.Debug.WriteLine(ex.ToString()); throw; }
			}
		}
	}
}
