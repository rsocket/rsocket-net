using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RSocket
{
	using System.Buffers;
	using System.Threading;

	public partial class RSocketProtocol
	{
		private const int INT24SIZE = (sizeof(UInt32) - 1);
		public const int FRAMELENGTHSIZE = INT24SIZE;
		private const int METADATALENGTHSIZE = INT24SIZE;

		//Memory Buffer Builders: ArrayPool<byte>
		//https://github.com/aspnet/SignalR/blob/2d4fb0af6fd2ef2e3a059a2a15a56484d8800d35/src/Common/MemoryBufferWriter.cs

		//TODO FEATURE TEST for Backpressure support with policy
		//TODO FEATURE KeepAlive
		//TODO FEATURE Lowest latency server support from client with heuristics.
		//TODO Feature Docs.
		//TODO Feature TLS for WebSockets, other?
		//TODO Feature QuickStart in the fashion of the existing ones
		//TODO Test Both WS and TCP with >Memory<T> buffer size. Since they don't accumulate in the state machine, they probably can overflow.

		public const int MESSAGEFRAMESIZE = INT24SIZE;  //This may be large than the FRAMELENGTHSIZE if the messages are padded inside of pipelines.
														//static public int MessageFrame(int length, bool isEndOfMessage) => isEndOfMessage ? length | (0b1 << sizeof(int) * 8 - 1) : length;	//High bit is EoM mark. Can't use twos-complement because negative zero is a legal value.
														//static public (int length, bool isEndofMessage) MessageFrame(int frame) => ((frame & ~(0b1 << sizeof(int) * 8 - 1)), (frame & (0b1 << sizeof(int) * 8 - 1)) != 0);
		static public (int Length, bool IsEndofMessage) MessageFrame(int frame) => (frame, true);
		static public int MessageFrame(int length, bool isEndOfMessage) => length;
		static public void MessageFrameWrite(int length, bool isEndOfMessage, Span<byte> target) { target[2] = (byte)((length >> 8 * 0) & 0xFF); target[1] = (byte)((length >> 8 * 1) & 0xFF); target[0] = (byte)((length >> 8 * 2) & 0xFF); }
		static public (int Length, bool IsEndOfMessage) MessageFramePeek(ReadOnlySequence<byte> sequence) { var reader = new SequenceReader<byte>(sequence); return reader.TryRead(out byte b1) && reader.TryRead(out byte b2) && reader.TryRead(out byte b3) ? ((b1 << 8 * 2) | (b2 << 8 * 1) | (b3 << 8 * 0), true) : (0, false); }

		static Task Flush(PipeWriter pipe, CancellationToken cancel)
		{
			var result = pipe.FlushAsync(cancel);
			Task task = result.IsCompleted ? Task.CompletedTask : result.AsTask();
			return task;
		}

		static bool TryReadRemaining(in Header header, int innerlength, ref SequenceReader<byte> reader, out int metadatalength)
		{
			//TODO Should assert that mdl = header.remain == 0;
			if (!header.HasMetadata) { metadatalength = 0; return true; } else { metadatalength = header.Remaining - innerlength; return true; }
		}

		static bool TryReadRemaining(in Header header, int innerlength, ref SequenceReader<byte> reader, out int metadatalength, out int datalength)
		{
			if (!header.HasMetadata) { metadatalength = 0; }// datalength = header.Remaining - innerlength - header.HasMetadataHeaderLength - metadatalength; return true; }
			else if (reader.TryReadUInt24BigEndian(out int length)) { metadatalength = length; }// datalength = header.Remaining - innerlength - METADATALENGTHSIZE - metadatalength; return true; }
			else { metadatalength = default; datalength = default; return false; }
			datalength = header.Remaining - innerlength - header.MetadataHeaderLength - metadatalength;
			return true;
		}



		public struct Payload
		{
			public const ushort FLAG_FOLLOWS = 0b____00_10000000;
			public const ushort FLAG_COMPLETE = 0b___00_01000000;
			public const ushort FLAG_NEXT = 0b_______00_00100000;
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }
			public bool HasFollows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }
			public bool IsComplete { get => (Header.Flags & FLAG_COMPLETE) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_COMPLETE) : (ushort)(Header.Flags & ~FLAG_COMPLETE); }
			public bool IsNext { get => (Header.Flags & FLAG_NEXT) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_NEXT) : (ushort)(Header.Flags & ~FLAG_NEXT); }

			private Header Header;
			public Int32 Stream => Header.Stream;
			public int MetadataLength;
			public int DataLength;
			private const int InnerLength = 0;
			public int Length => Header.Length + InnerLength + Header.MetadataHeaderLength + MetadataLength + DataLength;


			public Payload(int stream, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, bool follows = false, bool complete = false, bool next = false)    //TODO Parameter ordering, isn't Next much more likely than C or F?
			{
				Header = new Header(Types.Payload, stream, metadata: metadata);
				DataLength = (int)data.Length;
				MetadataLength = (int)metadata.Length;
				//TODO Assign HasMetadata based on this??? And everywhere.
				HasFollows = follows;
				IsComplete = complete;
				IsNext = next;
			}

			public Payload(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				TryReadRemaining(header, InnerLength, ref reader, out MetadataLength, out DataLength);
				//if (header.HasMetadata)
				//{
				//	reader.TryReadUInt24BigEndian(out int length);
				//	MetadataLength = length;
				//	DataLength = framelength - header.Length - (sizeof(int) - 1) - MetadataLength;
				//}
				//else { MetadataLength = 0; DataLength = framelength - header.Length - MetadataLength; }
			}

			public bool Validate(bool canContinue = false)
			{
				if (MetadataLength > MaxMetadataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(MetadataLength), MetadataLength, $"Invalid {nameof(Payload)} Message."); }
				//if (DataLength > MaxDataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(DataLength), DataLength, $"Invalid {nameof(Payload)} Message."); }
				//if (metadatalength != framelength - (Header.Length + payloadlength)) { }    //SPEC: Metadata Length MUST be equal to the Frame Length minus the sum of the length of the Frame Header and the length of the Frame Payload, if present.
				//Not sure how to assert this. If the frame has a length, then the payload length is computed from the frame length, resulting in a tautology. If the frame has no length, then the payload length is just what is, so the "frame length" is just this formula in reverse. So can it ever be false?
				if (!IsComplete && !IsNext) { throw new InvalidOperationException($"{nameof(Payload)} Messages must have {nameof(IsNext)} or {nameof(IsComplete)}."); }   //SPEC: A PAYLOAD MUST NOT have both (C)complete and (N)ext empty (false).
				return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			{
				var writer = BufferWriter.Get(pipe);
				this.Write(writer, data: data, metadata: metadata);
				writer.Flush();
				BufferWriter.Return(writer);
			}
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data, metadata: metadata);
				return Flush(pipe, cancel);
			}

			void Write(BufferWriter writer, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			{
				var written = Header.Write(writer, Length);
				if (HasMetadata) { written += writer.WriteInt24BigEndian(MetadataLength) + writer.Write(metadata); }      //TODO Should this be UInt24? Probably, but not sure if it can actually overflow...
				written += writer.Write(data);
			}

			public ReadOnlySequence<byte> ReadMetadata(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Position, MetadataLength);
			public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(MetadataLength, reader.Position), DataLength);

			public override string ToString() => $"{Header.ToString()} {ToStringFlags()}, Metadata[{MetadataLength}], Data[{DataLength}]";
			string ToStringFlags() => Header.ToStringFlags(new[] { (HasFollows, nameof(HasFollows), string.Empty), (IsComplete, nameof(IsComplete), string.Empty), (IsNext, nameof(IsNext), string.Empty) });
		}


		public struct RequestChannel
		{
			public const ushort FLAG_FOLLOWS = 0b___00_10000000;
			public const ushort FLAG_COMPLETE = 0b___00_01000000;
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }
			public bool HasFollows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }
			public bool IsComplete { get => (Header.Flags & FLAG_COMPLETE) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_COMPLETE) : (ushort)(Header.Flags & ~FLAG_COMPLETE); }

			private Header Header;
			public Int32 Stream => Header.Stream;
			public Int32 InitialRequest;
			public int MetadataLength;
			public int DataLength;
			private const int InnerLength = sizeof(Int32);
			public int Length => Header.Length + InnerLength + Header.MetadataHeaderLength + MetadataLength + DataLength;


			public RequestChannel(Int32 id, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false, bool complete = false)
			{
				Header = new Header(Types.Request_Channel, stream: id, metadata: metadata);
				InitialRequest = initialRequest;
				DataLength = (int)data.Length;
				MetadataLength = (int)metadata.Length;
				HasFollows = follows;
			}

			public RequestChannel(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				reader.TryReadBigEndian(out int initialRequest); InitialRequest = initialRequest;
				TryReadRemaining(header, InnerLength, ref reader, out MetadataLength, out DataLength);
			}

			public bool Validate(bool canContinue = false)
			{
				if (Header.Stream == 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(Header.Stream), Header.Stream, $"Invalid {nameof(RequestChannel)} Message."); }
				if (MetadataLength > MaxMetadataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(MetadataLength), MetadataLength, $"Invalid {nameof(RequestChannel)} Message."); }
				if (InitialRequest <= 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(InitialRequest), InitialRequest, $"Invalid {nameof(RequestChannel)} Message."); }   //SPEC: Value MUST be > 0.
				else return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
			{
				var writer = BufferWriter.Get(pipe);
				this.Write(writer, data: data, metadata: metadata);
				writer.Flush();
				BufferWriter.Return(writer);
			}

			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data, metadata: metadata);
				return Flush(pipe, cancel);
			}

			int Write(BufferWriter writer, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
			{
				var written = Header.Write(writer, Length);
				written += writer.WriteInt32BigEndian(InitialRequest);
				if (HasMetadata) { written += writer.WriteInt24BigEndian(MetadataLength) + writer.Write(metadata); }
				written += writer.Write(data);
				return written;
			}

			public ReadOnlySequence<byte> ReadMetadata(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Position, MetadataLength);
			public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(MetadataLength, reader.Position), DataLength);
		}


		public struct RequestStream
		{
			public const ushort FLAG_FOLLOWS = 0b___00_10000000;    //TODO Consider standard flag positions... They aren't specced this way, but in practice they are always in the same spot...
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }
			public bool HasFollows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }

			private Header Header;
			public Int32 Stream => Header.Stream;
			public Int32 InitialRequest;
			public int MetadataLength;
			public int DataLength;
			private const int InnerLength = sizeof(Int32);
			public int Length => Header.Length + InnerLength + Header.MetadataHeaderLength + MetadataLength + DataLength;


			public RequestStream(Int32 id, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false)
			{
				Header = new Header(Types.Request_Stream, stream: id, metadata: metadata);
				InitialRequest = initialRequest;
				DataLength = (int)data.Length;
				MetadataLength = (int)metadata.Length;
				HasFollows = follows;
			}

			public RequestStream(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				reader.TryReadBigEndian(out int initialRequest); InitialRequest = initialRequest;
				TryReadRemaining(header, InnerLength, ref reader, out MetadataLength, out DataLength);
			}

			public bool Validate(bool canContinue = false)
			{
				if (Header.Stream == 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(Header.Stream), Header.Stream, $"Invalid {nameof(RequestStream)} Message."); }
				if (MetadataLength > MaxMetadataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(MetadataLength), MetadataLength, $"Invalid {nameof(RequestStream)} Message."); }
				if (InitialRequest <= 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(InitialRequest), InitialRequest, $"Invalid {nameof(RequestStream)} Message."); }   //SPEC: Value MUST be > 0.
				else return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
			{
				var writer = BufferWriter.Get(pipe);
				this.Write(writer, data: data, metadata: metadata);
				writer.Flush();
				BufferWriter.Return(writer);
			}
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data, metadata: metadata);
				return Flush(pipe, cancel);
			}

			int Write(BufferWriter writer, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
			{
				var written = Header.Write(writer, Length);
				written += writer.WriteInt32BigEndian(InitialRequest);
				if (HasMetadata) { written += writer.WriteInt24BigEndian(MetadataLength) + writer.Write(metadata); }      //TODO Should this be UInt24? Probably, but not sure if it can actually overflow...
				written += writer.Write(data);
				return written;
			}

			public ReadOnlySequence<byte> ReadMetadata(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Position, MetadataLength);
			public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(MetadataLength, reader.Position), DataLength);
		}


		public struct RequestResponse
		{
			public const ushort FLAG_FOLLOWS = 0b___00_10000000;
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }
			public bool HasFollows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }

			private Header Header;
			public Int32 Stream => Header.Stream;
			public int MetadataLength;
			public int DataLength;
			private const int InnerLength = 0;
			public int Length => Header.Length + InnerLength + Header.MetadataHeaderLength + MetadataLength + DataLength;

			public RequestResponse(Int32 id, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false)
			{
				Header = new Header(Types.Request_Response, stream: id, metadata: metadata);
				DataLength = (int)data.Length;
				MetadataLength = (int)metadata.Length;
				HasFollows = follows;
			}

			public RequestResponse(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				TryReadRemaining(header, InnerLength, ref reader, out MetadataLength, out DataLength);
			}

			public bool Validate(bool canContinue = false)
			{
				if (Header.Stream == 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(Header.Stream), Header.Stream, $"Invalid {nameof(RequestResponse)} Message."); }
				if (MetadataLength > MaxMetadataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(MetadataLength), MetadataLength, $"Invalid {nameof(RequestResponse)} Message."); }
				else return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { var writer = BufferWriter.Get(pipe); this.Write(writer, data: data, metadata: metadata); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data, metadata: metadata);
				return Flush(pipe, cancel);
			}

			int Write(BufferWriter writer, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
			{
				var written = Header.Write(writer, Length);
				if (HasMetadata) { written += writer.WriteInt24BigEndian(MetadataLength) + writer.Write(metadata); }      //TODO Should this be UInt24? Probably, but not sure if it can actually overflow...
				written += writer.Write(data);
				return written;
			}

			public ReadOnlySequence<byte> ReadMetadata(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Position, MetadataLength);
			public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(MetadataLength, reader.Position), DataLength);
		}


		public struct RequestFireAndForget
		{
			public const ushort FLAG_FOLLOWS = 0b___00_10000000;
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }
			public bool HasFollows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }

			private Header Header;
			public Int32 Stream => Header.Stream;
			public int MetadataLength;
			public int DataLength;
			private const int InnerLength = 0;
			public int Length => Header.Length + InnerLength + Header.MetadataHeaderLength + MetadataLength + DataLength;

			public RequestFireAndForget(Int32 id, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false)
			{
				Header = new Header(Types.Request_Fire_And_Forget, stream: id, metadata: metadata);
				DataLength = (int)data.Length;
				MetadataLength = (int)metadata.Length;
				HasFollows = follows;
			}

			public RequestFireAndForget(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				TryReadRemaining(header, InnerLength, ref reader, out MetadataLength, out DataLength);
			}

			public bool Validate(bool canContinue = false)
			{
				if (Header.Stream == 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(Header.Stream), Header.Stream, $"Invalid {nameof(RequestFireAndForget)} Message."); }
				if (MetadataLength > MaxMetadataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(MetadataLength), MetadataLength, $"Invalid {nameof(RequestFireAndForget)} Message."); }
				else return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { var writer = BufferWriter.Get(pipe); this.Write(writer, data: data, metadata: metadata); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data, metadata: metadata);
				return Flush(pipe, cancel);
			}

			int Write(BufferWriter writer, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
			{
				var written = Header.Write(writer, Length);
				if (HasMetadata) { written += writer.WriteInt24BigEndian(MetadataLength) + writer.Write(metadata); }      //TODO Should this be UInt24? Probably, but not sure if it can actually overflow...
				written += writer.Write(data);
				return written;
			}

			public ReadOnlySequence<byte> ReadMetadata(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Position, MetadataLength);
			public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(MetadataLength, reader.Position), DataLength);
		}


		public struct RequestN
		{
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }

			private Header Header;
			public Int32 Stream => Header.Stream;
			public Int32 RequestNumber;
			private const int InnerLength = sizeof(Int32);
			public int Length => Header.Length + InnerLength;

			public RequestN(Int32 id, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false)
			{
				Header = new Header(Types.Request_N, stream: id, metadata: metadata);
				RequestNumber = initialRequest;
			}

			public RequestN(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				reader.TryReadBigEndian(out int requestNumber); RequestNumber = requestNumber;
			}

			public bool Validate(bool canContinue = false)
			{
				if (Header.Stream == 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(Header.Stream), Header.Stream, $"Invalid {nameof(RequestN)} Message."); }
				if (HasMetadata) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(HasMetadata), HasMetadata, $"Invalid {nameof(RequestN)} Message."); }
				if (RequestNumber <= 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(RequestNumber), RequestNumber, $"Invalid {nameof(RequestN)} Message."); }   //SPEC: Value MUST be > 0.
				else return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { var writer = BufferWriter.Get(pipe); this.Write(writer, data: data, metadata: metadata); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data, metadata: metadata);
				return Flush(pipe, cancel);
			}

			int Write(BufferWriter writer, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default)
			{
				var written = Header.Write(writer, Length);
				written += writer.WriteInt32BigEndian(RequestNumber);
				return written;
			}
		}


		public struct Cancel
		{
			private Header Header;
			public Int32 Stream => Header.Stream;
			public int Length => Header.Length;

			public Cancel(Int32 request)
			{
				Header = new Header(Types.Cancel, request);
			}

			public Cancel(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
			}

			public bool Validate(bool canContinue = false)
			{
				return true;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, CancellationToken cancel = default)
			{
				Write(pipe);
				return Flush(pipe, cancel);
			}

			void Write(BufferWriter writer)
			{
				var written = Header.Write(writer, Length);
			}
		}


		public struct KeepAlive
		{
			public const ushort FLAG_RESPOND = 0b__00_10000000;
			public bool Respond { get => (Header.Flags & FLAG_RESPOND) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_RESPOND) : (ushort)(Header.Flags & ~FLAG_RESPOND); }

			private Header Header;
			public Int64 LastReceivedPosition;
			public int DataLength;
			private const int InnerLength = sizeof(Int64);
			public int Length => Header.Length + InnerLength + DataLength;


			public KeepAlive(Int32 lastReceivedPosition, bool respond, ReadOnlySequence<byte> data = default)
			{
				Header = new Header(Types.KeepAlive);
				LastReceivedPosition = lastReceivedPosition;
				DataLength = (int)data.Length;
				Respond = respond;
			}

			public KeepAlive(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				reader.TryRead(out long lastReceivedPosition); LastReceivedPosition = lastReceivedPosition;
				//TODO Check this: DataLength = framelength - header.Length - sizeof(long);
				TryReadRemaining(header, InnerLength, ref reader, out _, out DataLength);
			}

			public bool Validate(bool canContinue = false)
			{
				if (Header.Stream == 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(Header.Stream), $"Invalid {nameof(KeepAlive)} Message."); }     //SPEC: KEEPALIVE frames MUST always use Stream ID 0 as they pertain to the Connection.
				if (LastReceivedPosition < 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(LastReceivedPosition), LastReceivedPosition, $"Invalid {nameof(KeepAlive)} Message."); }  //SPEC: Value MUST be > 0. (optional. Set to all 0s when not supported.)
				else return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data = default) { var writer = BufferWriter.Get(pipe); this.Write(writer, data: data); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data);
				return Flush(pipe, cancel);
			}

			void Write(BufferWriter writer, ReadOnlySequence<byte> data)
			{
				var written = Header.Write(writer, Length);
				written += writer.WriteInt64BigEndian(LastReceivedPosition);
				written += writer.Write(data);
			}

			public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(sizeof(long), reader.Position), DataLength);
		}


		public struct Lease
		{
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }

			private Header Header;
			public Int32 TimeToLive;
			public Int32 NumberOfRequests;
			public int MetadataLength;
			private const int InnerLength = sizeof(Int32) + sizeof(Int32);
			public int Length => Header.Length + InnerLength + MetadataLength;


			public Lease(Int32 timeToLive, Int32 numberOfRequests, ReadOnlySequence<byte> metadata)
			{
				Header = new Header(Types.Lease, metadata: metadata);
				TimeToLive = timeToLive;
				NumberOfRequests = numberOfRequests;
				MetadataLength = (int)metadata.Length;
			}

			public Lease(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				reader.TryRead(out int timeToLive); TimeToLive = timeToLive;
				reader.TryRead(out int numberOfRequests); NumberOfRequests = numberOfRequests;
				TryReadRemaining(header, InnerLength, ref reader, out MetadataLength);       //SPEC: This frame only supports Metadata, so the Metadata Length header MUST NOT be included, even if the(M)etadata flag is set true.
																							 //MetadataLength = header.HasMetadata ? MetadataLength = framelength - header.Length - sizeof(int) - sizeof(int) : 0;          //SPEC: This frame only supports Metadata, so the Metadata Length header MUST NOT be included, even if the(M)etadata flag is set true.
			}

			public bool Validate(bool canContinue = false)
			{
				if (Header.Stream == 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(Header.Stream), $"Invalid {nameof(Lease)} Message."); }
				if (MetadataLength > MaxMetadataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(MetadataLength), MetadataLength, $"Invalid {nameof(Lease)} Message."); }
				else return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> metadata) { var writer = BufferWriter.Get(pipe); this.Write(writer, metadata: metadata); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, metadata: metadata);
				return Flush(pipe, cancel);
			}

			void Write(BufferWriter writer, ReadOnlySequence<byte> metadata)
			{
				var written = Header.Write(writer, Length);
				written += writer.WriteInt32BigEndian(TimeToLive);
				written += writer.WriteInt32BigEndian(NumberOfRequests);
				if (HasMetadata) { written += writer.Write(metadata); }
			}
		}


		public struct Extension
		{
			public bool CanIgnore { get => Header.CanIgnore; set => Header.CanIgnore = value; }
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }

			private Header Header;
			public Int32 ExtendedType;
			public int ExtraLength;
			private const int InnerLength = sizeof(Int64);
			public int Length => Header.Length + InnerLength + ExtraLength;


			public Extension(Int32 extendedtype, bool ignore = false)
			{
				Header = new Header(Types.Extension);
				ExtendedType = extendedtype;
				ExtraLength = 0;
			}

			public Extension(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				reader.TryRead(out Int32 extendedType); ExtendedType = extendedType;
				TryReadRemaining(header, InnerLength, ref reader, out _, out ExtraLength);
			}

			public bool Validate(bool canContinue = false)
			{
				if (ExtendedType <= 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(ExtendedType), $"Invalid {nameof(Extension)} Message."); }
				return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> extra) { var writer = BufferWriter.Get(pipe); this.Write(writer, extra: extra); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> extra, CancellationToken cancel = default)
			{
				Write(pipe, extra: extra);
				return Flush(pipe, cancel);
			}

			void Write(BufferWriter writer, ReadOnlySequence<byte> extra)
			{
				var written = Header.Write(writer, Length);
				written += writer.WriteInt32BigEndian(ExtendedType);
				written += writer.Write(extra);
			}
		}


		public struct MetadataPush
		{
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }

			private Header Header;
			public int MetadataLength;
			private const int InnerLength = 0;
			public int Length => Header.Length + InnerLength + MetadataLength;

			public MetadataPush(ReadOnlySequence<byte> metadata)
			{
				Header = new Header(Types.Metadata_Push, metadata: metadata);
				MetadataLength = (int)metadata.Length;
			}

			public MetadataPush(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				TryReadRemaining(header, InnerLength, ref reader, out MetadataLength);       //SPEC: This frame only supports Metadata, so the Metadata Length header MUST NOT be included.
																							 //MetadataLength = header.HasMetadata ? MetadataLength = framelength - header.Length : 0; //SPEC: This frame only supports Metadata, so the Metadata Length header MUST NOT be included.
			}

			public bool Validate(bool canContinue = false)
			{
				if (Header.Stream == 0) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(Header.Stream), Header.Stream, $"Invalid {nameof(MetadataPush)} Message."); }
				if (MetadataLength > MaxMetadataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(MetadataLength), MetadataLength, $"Invalid {nameof(MetadataPush)} Message."); }
				else return true;
			}

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> metadata = default) { var writer = BufferWriter.Get(pipe); this.Write(writer, metadata: metadata); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, metadata: metadata);
				return Flush(pipe, cancel);
			}

			void Write(BufferWriter writer, ReadOnlySequence<byte> metadata = default)
			{
				var written = Header.Write(writer, Length);
				if (HasMetadata) { written += writer.Write(metadata); }
			}
		}


		public struct Error
		{
			private Header Header;
			public ErrorCodes ErrorCode;
			public int DataLength;
			public string ErrorText;
			private const int InnerLength = sizeof(Int32);
			public int Length => Header.Length + InnerLength + DataLength;
			public Int32 Stream => Header.Stream;

			public Error(ErrorCodes code, Int32 stream = Header.DEFAULT_STREAM, ReadOnlySequence<byte> data = default, string errorText = null)
			{
				Header = new Header(Types.Error, stream: stream);
				ErrorCode = code;
				DataLength = (int)data.Length;
				ErrorText = errorText;
			}

			public Error(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				reader.TryReadBigEndian(out Int32 errorCode); ErrorCode = (ErrorCodes)errorCode;
				TryReadRemaining(header, InnerLength, ref reader, out _, out DataLength);
				reader.TryRead(out string text, DataLength); ErrorText = text;
			}

			public bool Validate(bool canContinue = false) => true;

			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data = default) { var writer = BufferWriter.Get(pipe); this.Write(writer, data: data); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data);
				return Flush(pipe, cancel);
			}

			void Write(BufferWriter writer, ReadOnlySequence<byte> data = default)
			{
				var written = Header.Write(writer, Length);
				written += writer.WriteInt32BigEndian((int)ErrorCode);
				written += writer.Write(data);
			}

			public override string ToString() => $"{Header.ToString()} {ToStringFlags()}: [{ErrorCode:X}] {ErrorText}";
			string ToStringFlags() => Header.ToStringFlags();
		}


		public struct Setup
		{
			public const ushort FLAG_METADATA = 0b__01_00000000;
			public const ushort FLAG_RESUME = 0b____00_10000000;
			public const ushort FLAG_LEASE = 0b_____00_01000000;
			public bool HasMetadata { get => Header.HasMetadata; set => Header.HasMetadata = value; }
			public bool HasResume { get => (Header.Flags & FLAG_RESUME) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_RESUME) : (ushort)(Header.Flags & ~FLAG_RESUME); }
			public bool CanLease { get => (Header.Flags & FLAG_LEASE) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_LEASE) : (ushort)(Header.Flags & ~FLAG_LEASE); }

			private Header Header;
			public Int32 Flags => Header.Flags;
			public Int32 Stream => Header.Stream;
			public UInt16 MajorVersion;
			public UInt16 MinorVersion;
			public Int32 KeepAlive;
			public Int32 Lifetime;
			public byte[] ResumeToken;
			public string MetadataMimeType;
			public string DataMimeType;
			public int MetadataLength;
			public int DataLength;
			private int InnerLength => sizeof(UInt16) + sizeof(UInt16) + sizeof(Int32) + sizeof(Int32)
				+ (HasResume ? (ResumeToken.Length + sizeof(UInt16)) : 0)
				+ sizeof(byte) + Encoding.ASCII.GetByteCount(MetadataMimeType)
				+ sizeof(byte) + Encoding.ASCII.GetByteCount(DataMimeType);
			public int Length => Header.Length + InnerLength + Header.MetadataHeaderLength + MetadataLength + DataLength;


			public Setup(TimeSpan keepalive, TimeSpan lifetime, string metadataMimeType = null, string dataMimeType = null, byte[] resumeToken = default, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default) : this((int)keepalive.TotalMilliseconds, (int)lifetime.TotalMilliseconds, string.IsNullOrEmpty(metadataMimeType) ? string.Empty : metadataMimeType, string.IsNullOrEmpty(dataMimeType) ? string.Empty : dataMimeType, resumeToken: resumeToken, data: data, metadata: metadata) { }

			public Setup(Int32 keepalive, Int32 lifetime, string metadataMimeType, string dataMimeType, byte[] resumeToken = default, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			{
				Header = new Header(Types.Setup, metadata: metadata);
				MajorVersion = MAJOR_VERSION;
				MinorVersion = MINOR_VERSION;
				KeepAlive = keepalive;
				Lifetime = lifetime;
				ResumeToken = resumeToken;
				MetadataMimeType = metadataMimeType;
				DataMimeType = dataMimeType;
				ResumeToken = resumeToken;      //TODO Two of these?
				MetadataLength = (int)metadata.Length;
				DataLength = (int)data.Length;
				HasResume = resumeToken != default && resumeToken.Length > 0;
			}

			public Setup(in Header header, ref SequenceReader<byte> reader)
			{
				Header = header;
				reader.TryReadBigEndian(out UInt16 majorVersion); MajorVersion = majorVersion;
				reader.TryReadBigEndian(out UInt16 minorVersion); MinorVersion = minorVersion;
				reader.TryReadBigEndian(out Int32 keepAlive); KeepAlive = keepAlive;
				reader.TryReadBigEndian(out Int32 lifetime); Lifetime = lifetime;
				if ((header.Flags & FLAG_RESUME) != 0)      //TODO Duplicate test logic here
				{
					reader.TryReadBigEndian(out UInt16 resumeTokenLength);
					ResumeToken = new byte[resumeTokenLength];
					reader.TryRead(ResumeToken.AsSpan());
				}
				else { ResumeToken = Array.Empty<byte>(); }

				var mmtr = reader.TryReadPrefix(out MetadataMimeType);
				var dmtr = reader.TryReadPrefix(out DataMimeType);

				MetadataLength = DataLength = 0;    //Initialize so we can use InnerLength.
				TryReadRemaining(header, InnerLength, ref reader, out MetadataLength, out DataLength);
			}


			public bool Validate(bool canContinue = false)
			{
				if (MetadataLength > MaxMetadataLength) { return canContinue ? false : throw new ArgumentOutOfRangeException(nameof(MetadataLength), MetadataLength, $"Invalid {nameof(Setup)} Message."); }
				//TODO More validation here. See spec.
				return true;
			}

			//TODO So common, should be library..?
			public void Write(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default) { var writer = BufferWriter.Get(pipe); this.Write(writer, data: data, metadata: metadata); writer.Flush(); BufferWriter.Return(writer); }
			public Task WriteFlush(PipeWriter pipe, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, CancellationToken cancel = default)
			{
				Write(pipe, data: data, metadata: metadata);
				return Flush(pipe, cancel);
			}

			void Write(BufferWriter writer, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
			{
				var written = Header.Write(writer, Length);
				written += writer.WriteUInt16BigEndian(MajorVersion);
				written += writer.WriteUInt16BigEndian(MinorVersion);
				written += writer.WriteInt32BigEndian(KeepAlive);
				written += writer.WriteInt32BigEndian(Lifetime);
				if (HasResume)
				{
					written += writer.WriteUInt16BigEndian(ResumeToken.Length);
					written += writer.Write(ResumeToken);
				}
				written += writer.WritePrefixByte(MetadataMimeType);    //TODO THIS IS ASCII!!! See Spec!!
				written += writer.WritePrefixByte(DataMimeType);       //TODO THIS IS ASCII!!! See Spec!!
				if (HasMetadata) { written += writer.WriteInt24BigEndian(MetadataLength) + writer.Write(metadata); }      //TODO Should this be UInt24? Probably, but not sure if it can actually overflow...
				written += writer.Write(data);
			}

			public ReadOnlySequence<byte> ReadMetadata(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Position, MetadataLength);
			public ReadOnlySequence<byte> ReadData(in SequenceReader<byte> reader) => reader.Sequence.Slice(reader.Sequence.GetPosition(MetadataLength, reader.Position), DataLength);
			public void Read(ref SequenceReader<byte> reader, out ReadOnlySequence<byte> metadata, out ReadOnlySequence<byte> data) { metadata = ReadMetadata(reader); data = ReadData(reader); reader.Advance(metadata.Length + data.Length); }
		}


		public struct Header
		{
			public const Int32 DEFAULT_STREAM = 0;
			internal const int FRAMETYPE_OFFSET = 10;
			internal const ushort FRAMETYPE_TYPE = 0b_111111 << FRAMETYPE_OFFSET;
			internal const ushort FLAGS = 0b__________11_11111111;
			internal const ushort FLAG_IGNORE = 0b____10_00000000;
			internal const ushort FLAG_METADATA = 0b__01_00000000;
			public bool CanIgnore { get => (Flags & FLAG_IGNORE) != 0; set => Flags = value ? (ushort)(Flags | FLAG_IGNORE) : (ushort)(Flags & ~FLAG_IGNORE); }
			public bool HasMetadata { get => (Flags & FLAG_METADATA) != 0; set => Flags = value ? (ushort)(Flags | FLAG_METADATA) : (ushort)(Flags & ~FLAG_METADATA); }
			public int MetadataHeaderLength => HasMetadata ? METADATALENGTHSIZE : 0;        //TODO Only here?

			public Int32 Stream;
			public Types Type;
			public UInt16 Flags;
			static public Types MakeType(ushort flags) => (Types)((flags & Header.FRAMETYPE_TYPE) >> Header.FRAMETYPE_OFFSET);
			static public ushort MakeFlags(ushort flags) => (ushort)(flags & Header.FLAGS);
			public int Length => sizeof(Int32) + sizeof(UInt16);

			private int FrameLength;
			public int Remaining => FrameLength - Length;       //TODO Temporary refactoring

			public Header(Types type, Int32 stream = 0, in ReadOnlySequence<byte> metadata = default)
			{
				FrameLength = 0;
				Type = type;
				Stream = stream;
				Flags = 0;
				HasMetadata = metadata.Length > 0;
			}

			public Header(ref SequenceReader<byte> reader, int framelength = 0)
			{
				FrameLength = framelength;
				reader.TryReadBigEndian(out Stream);
				reader.TryReadBigEndian(out UInt16 flags);
				Type = MakeType(flags);
				Flags = MakeFlags(flags);
			}

			public int Write(BufferWriter writer, int length)
			{
				writer.WriteInt24BigEndian(length);     //Not included in total length.
				writer.WriteInt32BigEndian(Stream);
				writer.WriteUInt16BigEndian((((int)Type << FRAMETYPE_OFFSET) & FRAMETYPE_TYPE) | (Flags & FLAGS));//  (Ignore ? FLAG_IGNORE : 0) | (Metadata ? FLAG_METADATA : 0));
				return Length;
			}

			public override string ToString() => $"{Stream:0000} {Type}";
			public string ToStringFlags(IEnumerable<(bool, string, string)> flags = default) => new[] { (CanIgnore, nameof(CanIgnore), string.Empty), (HasMetadata, nameof(HasMetadata), string.Empty) }.Concat<(bool Condition, string True, string False)>(flags ?? Enumerable.Empty<(bool, string, string)>()).Aggregate(new StringBuilder($"{{{Flags:X3}"), (s, i) => s.Append(i.Condition ? "|" + i.True : i.False)).ToString().TrimEnd(',') + "}";
		}
	}
}
