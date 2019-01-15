using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace RSocket.RPC
{
	public class RemoteProcedureCall : IRSocketSerializer, IRSocketDeserializer
	{
		public const UInt16 VERSION = 1;

		public T Deserialize<T>(in ReadOnlySequence<byte> data)
		{
			throw new NotImplementedException();
		}

		public ReadOnlySequence<byte> Serialize<T>(in T item)	//TODO This is dumb, it should be writing to a BufferWriter or something else useful - this isn't great. Or maybe do both.
		{
			throw new NotImplementedException();
		}

		public ref struct RPCMetadata           //SPEC: https://github.com/rsocket/rsocket-rpc-java/blob/master/rsocket-rpc-core/src/main/java/io/rsocket/rpc/frames/Metadata.java
		{
			string Service;
			string Method;
			ReadOnlySpan<byte> Tracing;
			ReadOnlySpan<byte> Metadata;

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			int Write(BufferWriter writer)
			{
				var written = writer.WriteUInt16BigEndian(VERSION);
				written += writer.WritePrefixShort(Service);
				written += writer.WritePrefixShort(Method);
				written += writer.WritePrefixShort(Tracing);
				written += writer.Write(Metadata);
				return written;
			}
		}
	}
}
