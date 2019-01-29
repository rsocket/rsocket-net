using System;
using System.Buffers;
using System.Text;

namespace RSocket.RPC
{
	partial class RSocketService
	{
		ref struct RemoteProcedureCallMetadata           //SPEC: https://github.com/rsocket/rsocket-rpc-java/blob/master/rsocket-rpc-core/src/main/java/io/rsocket/rpc/frames/Metadata.java
		{
			public const UInt16 VERSION = 1;

			public string Service;
			public string Method;
			public ReadOnlySequence<byte> Tracing;
			public ReadOnlySequence<byte> Metadata;
			static public readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
			public int Length => sizeof(UInt16) + sizeof(UInt16) + DefaultEncoding.GetByteCount(Service) + sizeof(UInt16) + DefaultEncoding.GetByteCount(Method) + sizeof(UInt16) + (int)Tracing.Length + (int)Metadata.Length;

			public RemoteProcedureCallMetadata(string service, string method, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> tracing) { Service = service; Method = method; Metadata = metadata; Tracing = tracing; }

			public RemoteProcedureCallMetadata(ReadOnlySequence<byte> metadata)
			{
				var reader = new SequenceReader<byte>(metadata);
				if (!reader.TryReadBigEndian(out UInt16 version)) { throw new ArgumentOutOfRangeException(nameof(version), $"Invalid RPC Metadata."); }
				if (version != VERSION) { throw new ArgumentOutOfRangeException(nameof(version), $"Unsupported RPC Version {version}, expected {VERSION}."); }
				if (!reader.TryReadBigEndian(out UInt16 servicelength)) { throw new ArgumentOutOfRangeException(nameof(servicelength), $"Invalid RPC Metadata."); }
				if (reader.TryRead(out string service, servicelength)) { Service = service; } else { throw new ArgumentOutOfRangeException(nameof(service), $"Invalid RPC Metadata."); }
				if (!reader.TryReadBigEndian(out UInt16 methodlength)) { throw new ArgumentOutOfRangeException(nameof(methodlength), $"Invalid RPC Metadata."); }
				if (reader.TryRead(out string method, methodlength)) { Method = method; } else { throw new ArgumentOutOfRangeException(nameof(method), $"Invalid RPC Metadata."); }
				if (!reader.TryReadBigEndian(out UInt16 tracinglength)) { throw new ArgumentOutOfRangeException(nameof(tracinglength), $"Invalid RPC Metadata."); }
				Tracing = reader.Sequence.Slice(reader.Position, tracinglength);
				reader.Advance(tracinglength);
				Metadata = reader.Sequence.Slice(reader.Position, reader.Remaining);
			}

			public static implicit operator ReadOnlySequence<byte>(RemoteProcedureCallMetadata _)
			{
				var memory = new Memory<byte>(new byte[_.Length]);      //FUTURE PERFORMANCE: Someday, maybe use a buffer pool instead of allocating. These are presumed small, but the string scan adds some overhead.
				_.Write(new BufferWriter(new Writer<byte>(memory), DefaultEncoding));
				return new ReadOnlySequence<byte>(memory);
			}

			int Write(BufferWriter writer)
			{
				var written = writer.WriteUInt16BigEndian(VERSION);
				written += writer.WritePrefixShort(Service);
				written += writer.WritePrefixShort(Method);
				written += writer.WritePrefixShort(Tracing);
				written += writer.Write(Metadata);
				return written;
			}

			private struct Writer<T> : IBufferWriter<T>
			{
				int Position;
				Memory<T> Buffer;
				public Writer(Memory<T> buffer) { Buffer = buffer; Position = 0; }
				void IBufferWriter<T>.Advance(int count) => Position += count;
				Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => Buffer.Slice(Position);
				Span<T> IBufferWriter<T>.GetSpan(int sizeHint) => Buffer.Slice(Position).Span;
			}
		}
	}
}