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
	public partial class RSocketProtocol
	{
		static partial void Operation(string message, long BufferLength) => Console.WriteLine($".... {message}{(BufferLength == -1 ? "" : $"    ({nameof(BufferLength)}: {BufferLength})")}");
		static partial void StateEnter(States state) => Console.Write($"{state}:");
		static partial void Decode(string message, States? newstate, long BufferLength) => Console.WriteLine($"{message}{(BufferLength == -1 ? "" : $" / (Buffer[{BufferLength}])")}{(newstate == null ? "" : $" ==> {newstate}")}");
	}

	partial class RSocketProtocol
	{
		static partial void Operation(string message, long BufferLength = -1);
		static partial void StateEnter(States state);
		static partial void Decode(string message, States? newstate = null, long BufferLength = -1);

		enum States
		{
			Default = 0,
			Length = 1, Stream = 2, FrameType = 3, Flags = 4,

			Header = 0x1_0000,
			Remaining = 0xFFFE,
			Done = 0xFFFF,
		}

		static public async Task Server(PipeReader reader, CancellationToken cancel, bool uselength = false)
		{
			var state = uselength ? States.Length : States.Stream;

			while (!cancel.IsCancellationRequested)
			{
				Operation("Reading Pipe");
				var read = await reader.ReadAsync(cancel);
				var buffer = read.Buffer;
				if (buffer.IsEmpty && read.IsCompleted) { Operation("Reading Complete"); break; }
				var position = buffer.Start;

				Int32 length;
				Int32 stream;


				//var header = new Header();

				Types type = 0;
				Boolean ignore = false;
				Boolean metadata = false;
				Byte flags = 0;

				//TODO Protocol assertions (0 bit in stream, etc)

				while (!buffer.IsEmpty)
				{
					StateEnter(state);
					switch (state)
					{
						case States.Remaining:  //Transition to this state to just dump everything the remaining for inspection.
							if (TryReadByte(buffer, ref position, out var remaining)) { Decode($" [{remaining:X2}] '{Encoding.ASCII.GetString(new[] { remaining })}'", BufferLength: buffer.Length); break; }
							else { goto Break; } //Break a loop from within a switch. This is the recommended way to do this in C# if you don't want to allocate a looping flag.
						case States.Length: if (TryReadInt24BigEndian(buffer, ref position, out length)) { state = States.Stream; Decode($" [{length}]", state, buffer.Length); break; } else { goto Break; }
						case States.Stream: if (TryReadInt32BigEndian(buffer, ref position, out stream)) { state = States.FrameType; Decode($" [0x{stream:X8}]", state, buffer.Length); break; } else { goto Break; }
						case States.FrameType:
							if (TryReadByte(buffer, ref position, out var frametype))
							{
								int frametype2 = frametype << 8;    //TODO New 16-bit operations here
								type = (Types)((frametype2 & Header.FRAMETYPE_TYPE) >> Header.FRAMETYPE_OFFSET);
								ignore = (frametype2 & Header.FLAG_IGNORE) != 0;
								metadata = (frametype2 & Header.FLAG_METADATA) != 0;
								state = States.Flags;
								Decode($" [{type} {(ignore ? "Ignore" : "NoIgnore")} {(metadata ? "Metadata" : "NoMetadata")}]", state, buffer.Length);
								break;
							}
							else { goto Break; }
						case States.Flags: if (TryReadByte(buffer, ref position, out flags)) { state = States.Remaining; Decode($" [{flags}]", state, buffer.Length); break; } else { goto Break; }
						default: throw new InvalidOperationException($"State Machine Overflow at {state}!");
					}
					buffer = buffer.Slice(position);
				}
				Break:
				Operation(buffer.IsEmpty ? "Recycling with Empty Buffer" : $"Recycling with Partial Buffer", BufferLength: buffer.Length);
				reader.AdvanceTo(position, buffer.End);
			}
			Operation("Completing Pipe");
			reader.Complete();
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
	}
}
