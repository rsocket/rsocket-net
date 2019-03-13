using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RSocket
{
	public sealed class BufferWriter
	{
		private IBufferWriter<byte> _bufferWriter;
		private Memory<byte> Memory;
		private int Used;
		private int Remaining => Memory.Length - Used;

		public Encoding Encoding { get; private set; }
		public Encoder Encoder { get; private set; }
		public int MaximumBytesPerChar { get; private set; }
		static public readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		public bool InUse => _bufferWriter != null;

		public BufferWriter(IBufferWriter<byte> writer, Encoding encoding)
		{
			_bufferWriter = writer;
			Memory = Memory<byte>.Empty;
			Used = 0;
			Encoding = encoding ?? DefaultEncoding;
			Encoder = Encoding.GetEncoder();
			MaximumBytesPerChar = Encoding.GetMaxByteCount(1);
		}

		public BufferWriter Reset(IBufferWriter<byte> writer = null)
		{
			_bufferWriter = writer;
			Memory = Memory<byte>.Empty;
			Used = 0;
			Encoder.Reset();
			return this;
		}


		//TODO This might be better for one-liners to return the Memory object... Possibly better as an inlinable return value than a class reference?
		private void EnsureBuffer(int needed)
		{
			var remaining = Memory.Length - Used;
			if (remaining < needed)
			{
				if (Used > 0) { _bufferWriter.Advance(Used); }
				Memory = _bufferWriter.GetMemory(needed);
				Used = 0;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Span<byte> GetBuffer(int needed)
		{
			EnsureBuffer(needed);
			return Memory.Span.Slice(Used, Memory.Length - Used);       //TODO Is this the right kind?
		}

		public (Memory<byte>, int) Frame() { EnsureBuffer(sizeof(int)); var frame = (Memory, Used); Used += sizeof(Int32); return frame; }
		public void Frame((Memory<byte>, int) frame, int value) { var (Memory, Used) = frame; BinaryPrimitives.WriteInt32BigEndian(Memory.Span.Slice(Used, Memory.Length - Used), value); }


		public void Write(byte value) { EnsureBuffer(sizeof(byte)); Memory.Span[Used++] = (byte)value; }        //Save Memory<T>.Slice overhead for performance

		public void WriteByte(byte value) => Write(value);
		public void WriteByte(int value) => WriteByte((byte)value);                                             //This is a convenience for calls that use binary operators which always return int

		public int WriteUInt16BigEndian(int value) => WriteUInt16BigEndian((UInt16)value);
		public int WriteUInt16BigEndian(UInt16 value) { BinaryPrimitives.WriteUInt16BigEndian(GetBuffer(sizeof(UInt16)), value); Used += sizeof(UInt16); return sizeof(UInt16); }

		//public int WriteUInt16LittleEndian(UInt16 value) { BinaryPrimitives.WriteUInt16LittleEndian(GetBuffer(sizeof(UInt16)), value); Used += sizeof(UInt16); return sizeof(UInt16); }

		public int WriteInt32BigEndian(Int32 value) { BinaryPrimitives.WriteInt32BigEndian(GetBuffer(sizeof(Int32)), value); Used += sizeof(Int32); return sizeof(Int32); }
		public int WriteInt64BigEndian(Int64 value) { BinaryPrimitives.WriteInt64BigEndian(GetBuffer(sizeof(Int64)), value); Used += sizeof(Int64); return sizeof(Int64); }
		public int WriteUInt32BigEndian(UInt32 value) { BinaryPrimitives.WriteUInt32BigEndian(GetBuffer(sizeof(UInt32)), value); Used += sizeof(UInt32); return sizeof(UInt32); }
		public int WriteInt24BigEndian(int value) { const int SIZEOF = 3; var span = GetBuffer(SIZEOF); span[0] = (byte)((value >> 16) & 0xFF); span[1] = (byte)((value >> 8) & 0xFF); span[2] = (byte)(value & 0xFF); Used += SIZEOF; return SIZEOF; }

		public int Write(byte[] values) { foreach (var value in values) { Write(value); } return values.Length; }   //TODO Buffer Slice Writer
		public int Write(ReadOnlySpan<byte> values) => Write(values.ToArray());   //TODO SpanWriter - I had this, where did it go? Maybe speed the Sequence writer too.
		public int Write(ReadOnlySequence<byte> values) { if (values.IsSingleSegment) { return Write(values.First.Span); } else { int count = 0; foreach (var memory in values) { count += Write(memory.Span); } return count; } }

		public int Write(string text) => Write(text.AsSpan(), Encoder, MaximumBytesPerChar);

		public int WritePrefixByte(string text)
		{
			var bytes = Encoding.GetByteCount(text);
			if (bytes > Byte.MaxValue) { throw new ArgumentOutOfRangeException(nameof(text), text, $"String encoding [{bytes}] would exceed the maximum prefix length. [{Byte.MaxValue}]"); }
			Write((Byte)bytes);
			return sizeof(Byte) + Write(text);
		}

		public int WritePrefixShort(string text)
		{
			var bytes = Encoding.GetByteCount(text);
			if (bytes > UInt16.MaxValue) { throw new ArgumentOutOfRangeException(nameof(text), text, $"String encoding [{bytes}] would exceed the maximum prefix length. [{UInt16.MaxValue}]"); }
			WriteUInt16BigEndian(bytes);
			return sizeof(UInt16) + Write(text);
		}

		public int WritePrefixShort(ReadOnlySpan<byte> buffer)
		{
			var bytes = buffer.Length;
			if (bytes > UInt16.MaxValue) { throw new ArgumentOutOfRangeException(nameof(buffer), buffer.Length, $"Buffer [{bytes}] would exceed the maximum prefix length. [{UInt16.MaxValue}]"); }
			WriteUInt16BigEndian(bytes);
			return sizeof(UInt16) + Write(buffer);
		}

		public int WritePrefixShort(ReadOnlySequence<byte> buffer)
		{
			var bytes = buffer.Length;
			if (bytes > UInt16.MaxValue) { throw new ArgumentOutOfRangeException(nameof(buffer), buffer.Length, $"Buffer [{bytes}] would exceed the maximum prefix length. [{UInt16.MaxValue}]"); }
			WriteUInt16BigEndian((UInt16)bytes);
			return sizeof(UInt16) + Write(buffer);
		}


		public unsafe void Write(char value, Encoder encoder, int encodingmaxbytesperchar)
		{
			var destination = GetBuffer(encodingmaxbytesperchar);

			var bytesUsed = 0;
			var charsUsed = 0;
#if NETCOREAPP2_2
            _encoder.Convert(new Span<char>(&value, 1), destination, false, out charsUsed, out bytesUsed, out _);
#else
			fixed (byte* destinationBytes = &MemoryMarshal.GetReference(destination))
			{
				encoder.Convert(&value, 1, destinationBytes, destination.Length, false, out charsUsed, out bytesUsed, out _);
			}
#endif

			System.Diagnostics.Debug.Assert(charsUsed == 1);
			Used += bytesUsed;
		}

		public int Write(ReadOnlySpan<char> source, Encoder encoder, int encodingmaxbytesperchar)
		{
			var length = 0;
			while (source.Length > 0)
			{
				var destination = GetBuffer(encodingmaxbytesperchar);

				var bytesUsed = 0;
				var charsUsed = 0;
#if NETCOREAPP2_2
                encoder.Convert(source, destination, false, out charsUsed, out bytesUsed, out _);
#else
				unsafe
				{
					fixed (char* sourceChars = &MemoryMarshal.GetReference(source))
					fixed (byte* destinationBytes = &MemoryMarshal.GetReference(destination))
					{
						encoder.Convert(sourceChars, source.Length, destinationBytes, destination.Length, false, out charsUsed, out bytesUsed, out _);
					}
				}
#endif
				source = source.Slice(charsUsed);
				Used += bytesUsed;
				length += bytesUsed;
			}
			return length;
		}

		//TextWriter Compatibility
		public void Write(char[] buffer, int index, int count) => Write(buffer.AsSpan(index, count), Encoder, MaximumBytesPerChar);
		public void Write(char[] buffer) => Write(buffer, Encoder, MaximumBytesPerChar);
		public void Write(char value) => Write(value, Encoder, MaximumBytesPerChar);

		public void Flush()
		{
			if (Used > 0)
			{
				_bufferWriter.Advance(Used);
				Memory = Memory.Slice(Used, Memory.Length - Used); //TODO Is this the right overload for this?
				Used = 0;
			}
		}


		[ThreadStatic]
		private static BufferWriter Instance;

		public static BufferWriter Get(IBufferWriter<byte> bufferWriter, Encoding encoding = null)
		{
			var writer = Instance ?? new BufferWriter(null, encoding); //Special initialization to track prior use.
			Instance = null; //Decache this on the thread
#if DEBUG
			if (writer.InUse) { throw new InvalidOperationException($"The {nameof(BufferWriter)} wasn't returned!"); }
#endif
			return writer.Reset(bufferWriter);
		}

		public static void Return(BufferWriter writer) => Instance = writer.Reset();
	}
}