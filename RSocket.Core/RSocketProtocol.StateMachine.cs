using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketProtocol
	{
		void Payload(in RSocketProtocol.Payload value);
	}

	public partial class RSocketProtocol
	{
		//static partial void OnPayload(in RSocketProtocol.Payload value) => Decode($"Got Payload: {Encoding.UTF8.GetString(value.Data)}");
		static partial void OnPayload(IRSocketProtocol sink, in RSocketProtocol.Payload value) => sink.Payload(value);
	}

	public partial class RSocketProtocol
	{
		static partial void Operation(string message, long BufferLength) => Console.WriteLine($".... {message}{(BufferLength == -1 ? "" : $"    ({nameof(BufferLength)}: {BufferLength})")}");
		static partial void StateEnter(States state) => Console.Write($"{{State.{state}}}");
		static partial void Decode(string message, States? newstate, long BufferLength) => Console.WriteLine($"{message}{(BufferLength == -1 ? "" : $" / (Buffer[{BufferLength}])")}{(newstate == null ? "" : $" ==> {newstate}")}");
	}

	partial class RSocketProtocol
	{
		static partial void Operation(string message, long BufferLength = -1);
		static partial void StateEnter(States state);
		static partial void Decode(string message, States? newstate = null, long BufferLength = -1);
		static partial void OnPayload(IRSocketProtocol sink, in RSocketProtocol.Payload value);

		static public int MessageFrame(int length, bool isEndOfMessage) => isEndOfMessage ? length | (0b1 << sizeof(int) * 8 - 1) : length;	//High bit is EoM mark. Can't use twos-complement because of negative zero.
		static public (int length, bool isEndofMessage) MessageFrame(int frame) => ((frame & ~(0b1 << sizeof(int) * 8 - 1)), (frame & (0b1 << sizeof(int) * 8 - 1)) != 0);

		enum States
		{
			Default = 0,
			Length = 1, Stream = 2, FrameType = 3, Flags = 4, TypeDispatch = 5,

			Header2 = 0x01_0000,
			Payload = 0x0A_0000, PayloadMetadata = 0x0A_0001, PayloadData = 0x0A_0002,
			Remaining = 0xFFFE,
			Done = 0xFFFF,
		}

		static public async Task Server(IRSocketProtocol sink, PipeReader reader, CancellationToken cancel)
		{
			var state = States.Length;

			while (!cancel.IsCancellationRequested)
			{
				Operation("Reading Pipe");
				var read = await reader.ReadAsync(cancel);
				var buffer = read.Buffer;
				if (buffer.IsEmpty && read.IsCompleted) { Operation("Reading Complete"); break; } else { Operation($"Got Buffer [{buffer.Length}]"); }

				while (!buffer.IsEmpty)
				{
					var position = buffer.Start;

					//if (!usePrefixFrameLength)  //If no prefix frame length, the underlying pipeline must use EoM framing.
					//{
					//	if (TryReadInt32BigEndian(buffer, ref position, out int frame))
					//	{
					//		var (framelength, isend) = MessageFrame(frame);
					//		buffer = buffer.Slice(position, framelength);
					//	}
					//	else { continue; }
					//}


					Int32 length;
					bool isEndOfMessage;
					Int32 stream = 0;
					Types type = 0;
					ushort flags = 0;

					//var header = new Header();

					//TODO Protocol assertions (0 bit in stream, etc)

					while (!buffer.IsEmpty)
					{
						StateEnter(state);
						switch (state)
						{
							case States.Remaining:  //Transition to this state to just dump everything the remaining for inspection.
								if (TryReadByte(buffer, ref position, out var remaining)) { Decode($" [{remaining:X2}] '{Encoding.ASCII.GetString(new[] { remaining })}'", BufferLength: buffer.Length); break; }
								else { goto Break; } //Break a loop from within a switch. This is the recommended way to do this in C# if you don't want to allocate a looping flag.
							case States.Length: if (TryReadInt32BigEndian(buffer, ref position, out var frame)) { (length, isEndOfMessage) = MessageFrame(frame); state = States.Stream; Decode($" [{length}, {isEndOfMessage:5}]", state, buffer.Length); break; } else { goto Break; }
							case States.Stream: if (TryReadInt32BigEndian(buffer, ref position, out stream)) { state = States.Flags; Decode($" [0x{stream:X8}]", state, buffer.Length); break; } else { goto Break; }
							case States.Flags:
								//if (TryReadByte(buffer, ref position, out flags)) { state = States.Remaining; Decode($" [{flags}]", state, buffer.Length); break; } else { goto Break; }
								if (TryReadUInt16BigEndian(buffer, ref position, out var _flags))
								{
									state = States.TypeDispatch;
									type = Header.MakeType(_flags);
									flags = Header.MakeFlags(_flags);
									Decode($" [{type} {(Header.HasIgnore(flags) ? "Ignore" : "NoIgnore")} {(Header.HasMetadata(flags) ? "Metadata" : "NoMetadata")} Flags ({flags:X3})]", state, buffer.Length);
									goto case States.TypeDispatch;
								}
								else { goto Break; }
							case States.TypeDispatch:
								switch (type)
								{
									case Types.Payload: state = States.Payload; break;
									default: throw new InvalidOperationException($"State Machine Invalid Type {type}!");
								}

								break;

							case States.Payload:
								//TODO !complete & !next fails.
								if (Payload.HasMetadata(flags)) goto case States.PayloadMetadata;
								if (Payload.HasNext(flags)) goto case States.PayloadData;

								//TODO verify with protocol.
								OnPayload(sink, new Payload(stream, Span<byte>.Empty, follows: Payload.HasFollows(flags), complete: Payload.HasComplete(flags), next: Payload.HasNext(flags)));
								break;
							case States.PayloadMetadata:
								break;
							case States.PayloadData:
								//TODO Allow zero byte payload
								if (TryReadAll(buffer, ref position, out var data))
								{
									OnPayload(sink, new Payload(stream, data, follows: Payload.HasFollows(flags), complete: Payload.HasComplete(flags), next: Payload.HasNext(flags)));
									goto case States.Done;
								}
								break;

							//public Payload(byte[] data, string metadata = null, bool follows = false, bool complete = false, bool next = false)

							case States.Done: state = States.Length; break;		//TODO isEOM handling...
							default: throw new InvalidOperationException($"State Machine Overflow at {state}!");
						}
						buffer = buffer.Slice(position);    //TODO goto case
					}
					Break:
					Operation(buffer.IsEmpty ? "Recycling with Empty Buffer" : $"Recycling with Partial Buffer", BufferLength: buffer.Length);
					reader.AdvanceTo(position, buffer.End);
				}
			}
			Operation("Completing Pipe");
			reader.Complete();
		}

		internal ref struct BufferReader
		{
			private ReadOnlySequence<byte> Sequence;
			private SequencePosition Position;
			private SequencePosition Next;

			private ReadOnlySpan<byte> Current;
			private int Index;
			public bool End { get; private set; }

			//private SequencePosition _nextSequencePosition;

			//private int _consumedBytes;

			//[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public BufferReader(in ReadOnlySequence<byte> buffer)
			{
				Sequence = buffer;
				Position = Sequence.Start;
				Index = 0;
				//_consumedBytes = 0;
				Next = Position;

				if (Sequence.TryGet(ref Next, out var memory, true))
				{
					End = false;
					Current = memory.Span;
					//if (Current.Length == 0) { MoveNext(); }
				}
				else
				{
					End = true;
					Current = default;
				}
			}

			//bool TryReadInt32BigEndian(in ReadOnlySequence<byte> buffer, out int value)
			//{
			//	var position = buffer.Start;
			//	//while (position < buffer.End)
			//	//{
			//	buffer.TryGet(ref position, out var memory, advance: false);
			//	var frame = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(memory.Span);
			//	position = buffer.GetPosition(sizeof(Int32), position);
			//	await socket.SendAsync(buffer.Slice(position, frame), _webSocketMessageType);
			//	position = buffer.GetPosition(frame, position);
			//	//}


			//	/// <summary>
			//	/// Converts the <see cref="ReadOnlySequence{T}"/> to an array
			//	/// </summary>
			//	public static T[] ToArray<T>(in this ReadOnlySequence<T> sequence)
			//	{
			//		var array = new T[sequence.Length];
			//		sequence.CopyTo(array);
			//		return array;
			//	}
			//}

			////[MethodImpl(MethodImplOptions.NoInlining)]
			//private void MoveNext()
			//{
			//	var previous = _nextSequencePosition;
			//	while (Sequence.TryGet(ref _nextSequencePosition, out var memory, true))
			//	{
			//		_currentSequencePosition = previous;
			//		Current = memory.Span; Index = 0;
			//		if (Current.Length > 0) { return; }
			//	}
			//	_end = true;
			//}
		}


		static bool TryReadAll(ReadOnlySequence<byte> buffer, ref SequencePosition position, out byte[] values)
		{
			buffer = buffer.Slice(position);
			if (buffer.IsEmpty) { values = Span<byte>.Empty.ToArray(); return true; }	//TODO garbage.
			if (buffer.TryGet(ref position, out var memory)) { values = memory.Span.ToArray(); position = buffer.GetPosition(values.Length); return true; }
			else { throw new InvalidOperationException(); }    //This is impossible, since we had at least one byte...
		}

		static bool TryReadByte(ReadOnlySequence<byte> buffer, ref SequencePosition position, out Byte value)
		{
			const int SIZEOF = sizeof(Byte);
			buffer = buffer.Slice(position);
			if (buffer.IsEmpty) { value = 0; return false; }        //TODO Look for Buffer.Length checks - probably bad since it would have to traverse the list.
			if (buffer.TryGet(ref position, out var memory)) { value = memory.Span[0]; position = buffer.GetPosition(SIZEOF); return true; }
			else { throw new InvalidOperationException(); }    //This is impossible, since we had at least one byte...
		}

		static bool TryReadInt24BigEndian(ReadOnlySequence<byte> buffer, ref SequencePosition position, out Int32 value)
		{
			const int SIZEOF = 3;
			buffer = buffer.Slice(position, SIZEOF);
			if (buffer.TryGet(ref position, out var memory))        //TODO is this better as IsSingleSegment or unrolled loop or even read 4 bytes and if (BinaryPrimitives.TryReadInt32BigEndian(span, out var result) { value = result & 0xFFFFFF; }
			{
				System.Diagnostics.Debug.Assert(memory.Length == SIZEOF);
				var span = memory.Span;
				value = (span[0]) | (span[1] << 8) | (span[2] << 16);
			}
			else
			{
				if (buffer.Length < SIZEOF) { value = 0; return false; }
				Int32 result = 0;
				foreach (var subbuffer in buffer)
				{
					for (int index = 0; index < subbuffer.Length; index++)
					{
						result = (result << 8 | subbuffer.Span[index]);
					}
				}
				value = result;
			}
			position = buffer.GetPosition(SIZEOF);
			return true;
		}

		static bool TryReadInt32BigEndian(ReadOnlySequence<byte> buffer, ref SequencePosition position, out Int32 value)
		{
			const int SIZEOF = sizeof(Int32);
			buffer = buffer.Slice(position, SIZEOF);

			if (!buffer.TryGet(ref position, out var memory) || !BinaryPrimitives.TryReadInt32BigEndian(memory.Span, out value))
			{
				if (buffer.Length < SIZEOF) { value = 0; return false; }

				Int32 result = 0;
				foreach (var subbuffer in buffer)
				{
					for (int index = 0; index < subbuffer.Length; index++)
					{
						result = (result << 8 | subbuffer.Span[index]);
					}
				}
				value = result;
			}
			position = buffer.GetPosition(SIZEOF);
			return true;
		}

		static bool TryReadUInt16BigEndian(ReadOnlySequence<byte> buffer, ref SequencePosition position, out UInt16 value)
		{
			const int SIZEOF = sizeof(UInt16);
			buffer = buffer.Slice(position, SIZEOF);

			if (!buffer.TryGet(ref position, out var memory) || !BinaryPrimitives.TryReadUInt16BigEndian(memory.Span, out value))
			{
				if (buffer.Length < SIZEOF) { value = 0; return false; }

				int result = 0;
				foreach (var subbuffer in buffer)
				{
					for (int index = 0; index < subbuffer.Length; index++)
					{
						result = (result << 8 | subbuffer.Span[index]);
					}
				}
				value = (UInt16)result;
			}
			position = buffer.GetPosition(SIZEOF);
			return true;
		}
	}
}
