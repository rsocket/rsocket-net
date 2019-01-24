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
	//public partial class RSocketProtocol
	//{
	//	static partial void Decoded(string message) => Console.WriteLine(message);
	//	//static partial void OnPayload(IRSocketProtocol sink, in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => Decoded($"    ===> Metadata[{metadata.Length}]:({Encoding.UTF8.GetString(metadata.ToArray())})    Data[{data.Length}]: {Encoding.UTF8.GetString(data.ToArray())}");
	//	static partial void OnSetup(IRSocketProtocol sink, in RSocketProtocol.Setup message) => sink.Setup(message);
	//	static partial void OnError(IRSocketProtocol sink, in RSocketProtocol.Error message) => sink.Error(message);
	//	static partial void OnPayload(IRSocketProtocol sink, in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => sink.Payload(message, metadata, data);
	//	static partial void OnRequestStream(IRSocketProtocol sink, in RSocketProtocol.RequestStream message) => sink.RequestStream(message);
	//}

	//public partial class RSocketProtocol
	//{
	//	static partial void Operation(string message, long BufferLength) => Console.WriteLine($".... {message}{(BufferLength == -1 ? "" : $"    ({nameof(BufferLength)}: {BufferLength})")}");
	//	static partial void StateEnter(States state) => Console.Write($"{{State.{state}}}");
	//	static partial void Decode(string message, States? newstate, long BufferLength, string name) => Console.WriteLine($"{name ?? string.Empty} {message}{(BufferLength == -1 ? "" : $" / (Buffer[{BufferLength}])")}{(newstate == null ? "" : $" ==> {newstate}")}");
	//}

	partial class RSocketProtocol
	{
		//static partial void Operation(string message, long BufferLength = -1);
		//static partial void StateEnter(States state);
		//static partial void Decode(string message, States? newstate = null, long BufferLength = -1, string name = null);
		//static partial void Decoded(string message);

		//static partial void OnSetup(IRSocketProtocol sink, in RSocketProtocol.Setup message);
		//static partial void OnError(IRSocketProtocol sink, in RSocketProtocol.Error message);
		//static partial void OnPayload(IRSocketProtocol sink, in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		//static partial void OnRequestStream(IRSocketProtocol sink, in RSocketProtocol.RequestStream message);

		//static public int MessageFrame(int length, bool isEndOfMessage) => isEndOfMessage ? length | (0b1 << sizeof(int) * 8 - 1) : length;	//High bit is EoM mark. Can't use twos-complement because negative zero is a legal value.
		//static public (int length, bool isEndofMessage) MessageFrame(int frame) => ((frame & ~(0b1 << sizeof(int) * 8 - 1)), (frame & (0b1 << sizeof(int) * 8 - 1)) != 0);

		static public async Task Handler2(IRSocketProtocol sink, PipeReader pipereader, CancellationToken cancellation, string name = null)
		{
			//The original implementation was a state-machine parser with resumability. It doesn't seem like the other implementations follow this pattern and the .NET folks are still figuring this out too - see the internal JSON parser discussion for how they're handling state machine persistence across async boundaries when servicing a Pipeline. So, this version of the handler only processes complete messages at some cost to memory buffer scalability.
			//Note that this means that the Pipeline must be configured to have enough buffering for a complete message before source-quenching. This also means that the downstream consumers don't really have to support resumption, so the interface no longer has the partial buffer methods in it.

			while (!cancellation.IsCancellationRequested)
			{
				var read = await pipereader.ReadAsync(cancellation);
				var buffer = read.Buffer;
				if (buffer.IsEmpty && read.IsCompleted) { break; }
				var position = buffer.Start;

				//Due to the nature of Pipelines as simple binary pipes, all Transport adapters assemble a standard message frame whether or not the underlying transport signals length, EoM, etc.
				if (!TryReadInt32BigEndian(buffer, ref position, out int frame)) { pipereader.AdvanceTo(buffer.Start, buffer.End); continue; }
				var (framelength, ismessageend) = MessageFrame(frame);
				if (buffer.Length < framelength) { pipereader.AdvanceTo(buffer.Start, buffer.End); continue; }  //Don't have a complete message yet. Tell the pipe that we've evaluated up to the current buffer end, but cannot yet consume it.

				Process(framelength, buffer.Slice(position));
				pipereader.AdvanceTo(buffer.GetPosition(framelength, position));
				//TODO - this should work now too!!! Need to evaluate if there is more than one packet in the pipe including edges like part of the length bytes are there but not all.
			}
			pipereader.Complete();

			
			void Process(int framelength, ReadOnlySequence<byte> sequence)
			{
				var reader = new SequenceReader<byte>(sequence);
				var header = new Header(ref reader, framelength);

				switch (header.Type)
				{
					case Types.Reserved: { throw new InvalidOperationException($"Protocol Reserved! [{header.Type}]"); }
					case Types.Setup:
						var setup = new Setup(header, ref reader);
						OnSetup(sink, setup);
						break;
					case Types.Lease:
						var lease = new Lease(header, ref reader);
						break;
					case Types.KeepAlive:
						var keepalive = new KeepAlive(header, ref reader);
						break;
					case Types.Request_Response:
						var requestresponse = new RequestResponse(header, ref reader);
						if (requestresponse.Validate()) { OnRequestResponse(sink, requestresponse, requestresponse.ReadMetadata(ref reader), requestresponse.ReadData(ref reader)); }
						break;
					case Types.Request_Fire_And_Forget:
						var requestfireandforget = new RequestFireAndForget(header, ref reader);
						if (requestfireandforget.Validate()) { OnRequestFireAndForget(sink, requestfireandforget, requestfireandforget.ReadMetadata(ref reader), requestfireandforget.ReadData(ref reader)); }
						break;
					case Types.Request_Stream:
						var requeststream = new RequestStream(header, ref reader);
						if (requeststream.Validate()) { OnRequestStream(sink, requeststream, requeststream.ReadMetadata(ref reader), requeststream.ReadData(ref reader)); }
						break;
					case Types.Request_Channel:
						var requestchannel = new RequestChannel(header, ref reader);
						if (requestchannel.Validate()) { OnRequestChannel(sink, requestchannel, requestchannel.ReadMetadata(ref reader), requestchannel.ReadData(ref reader)); }
						break;
					case Types.Request_N:
						var requestne = new RequestN(header, ref reader);
						break;
					case Types.Cancel:
						var cancel = new Cancel(header, ref reader);
						break;
					case Types.Payload:
						var payload = new Payload(header, ref reader);
						Decoded(payload.ToString());
						if (payload.Validate())
						{
							OnPayload(sink, payload, payload.ReadMetadata(ref reader), payload.ReadData(ref reader));
							//reader.Sequence.Slice(reader.Position, payload.MetadataLength), reader.Sequence.Slice(reader.Sequence.GetPosition(payload.MetadataLength, reader.Position), payload.DataLength));
						}
						break;
					case Types.Error:
						var error = new Error(header, ref reader);
						Decoded(error.ToString());
						OnError(sink, error);
						break;
					case Types.Metadata_Push:
						var metadatapush = new MetadataPush(header, ref reader);
						break;
					case Types.Resume: { throw new NotSupportedException($"Protocol Resumption not Supported. [{header.Type}]"); }
					case Types.Resume_OK: { throw new NotSupportedException($"Protocol Resumption not Supported. [{header.Type}]"); }
					case Types.Extension: if (!header.CanIgnore) { throw new InvalidOperationException($"Protocol Extension Unsupported! [{header.Type}]"); } else break;
					default: if (!header.CanIgnore) { throw new InvalidOperationException($"Protocol Unknown Type! [{header.Type}]"); } else break;
				}

				//while (!buffer.IsEmpty)
				//	{
				//		var position = buffer.Start;


				//		Int32 length;
				//		bool isEndOfMessage;
				//		Int32 stream = 0;
				//		Types type = 0;
				//		ushort flags = 0;

				//		//var header = new Header();

				//		//TODO Protocol assertions (0 bit in stream, etc)

				//		while (!buffer.IsEmpty)
				//		{
				//			StateEnter(state);
				//			switch (state)
				//			{
				//				case States.Remaining:  //Transition to this state to just dump everything the remaining for inspection.
				//					if (TryReadByte(buffer, ref position, out var remaining)) { Decode($" [{remaining:X2}] '{Encoding.ASCII.GetString(new[] { remaining })}'", BufferLength: buffer.Length, name: name); break; }
				//					else { goto Break; } //Break a loop from within a switch. This is the recommended way to do this in C# if you don't want to allocate a looping flag.
				//				case States.Length: if (TryReadInt32BigEndian(buffer, ref position, out var frame)) { (length, isEndOfMessage) = MessageFrame(frame); state = States.Stream; Decode($" [{length}, {isEndOfMessage:5}]", state, buffer.Length, name: name); break; } else { goto Break; }
				//				case States.Stream: if (TryReadInt32BigEndian(buffer, ref position, out stream)) { state = States.Flags; Decode($" [0x{stream:X8}]", state, buffer.Length, name: name); break; } else { goto Break; }
				//				case States.Flags:
				//					//if (TryReadByte(buffer, ref position, out flags)) { state = States.Remaining; Decode($" [{flags}]", state, buffer.Length); break; } else { goto Break; }
				//					if (TryReadUInt16BigEndian(buffer, ref position, out var _flags))
				//					{
				//						state = States.TypeDispatch;
				//						type = Header.MakeType(_flags);
				//						flags = Header.MakeFlags(_flags);
				//						Decode($" [{type} {(Header.HasIgnore(flags) ? "Ignore" : "NoIgnore")} {(Header.HasMetadata(flags) ? "Metadata" : "NoMetadata")} Flags ({flags:X3})]", state, buffer.Length, name: name);
				//						goto case States.TypeDispatch;
				//					}
				//					else { goto Break; }
				//				case States.TypeDispatch:
				//					switch (type)
				//					{
				//						case Types.Payload: state = States.Payload; break;
				//						default: throw new InvalidOperationException($"Handler State {state} Invalid Dispatch Type {type}!");
				//					}
				//					break;

				//				case States.Payload:
				//					//TODO !complete & !next fails.
				//					if (Payload.HasMetadata(flags)) goto case States.PayloadMetadata;
				//					if (Payload.HasNext(flags)) goto case States.PayloadData;

				//					//TODO verify with protocol.
				//					OnPayload(sink, new Payload(stream, Span<byte>.Empty, follows: Payload.HasFollows(flags), complete: Payload.HasComplete(flags), next: Payload.HasNext(flags)));
				//					break;
				//				case States.PayloadMetadata:
				//					break;
				//				case States.PayloadData:
				//					//TODO Allow zero byte payload
				//					if (TryReadAll(buffer, ref position, out var data))
				//					{
				//						OnPayload(sink, new Payload(stream, data, follows: Payload.HasFollows(flags), complete: Payload.HasComplete(flags), next: Payload.HasNext(flags)));
				//						goto case States.Done;
				//					}
				//					break;
				//				case States.Done: state = States.Length; break;     //TODO isEOM handling...
				//				default: throw new InvalidOperationException($"State Machine Overflow at {state}!");
				//			}
				//			buffer = buffer.Slice(position);    //TODO goto case
				//		}
				//		Break:
				//		Operation(buffer.IsEmpty ? $"{name} Recycling with Empty Buffer" : $"{name} Recycling with Partial Buffer", BufferLength: buffer.Length);
				//		pipereader.AdvanceTo(position, buffer.End);
				//	}
			}
		}

		/*
		enum States
		{
			Default = 0,
			Length = 1, Stream = 2, FrameType = 3, Flags = 4, TypeDispatch = 5,

			Header2 = 0x01_0000,
			Payload = 0x0A_0000, PayloadMetadata = 0x0A_0001, PayloadData = 0x0A_0002,
			Remaining = 0xFFFE,
			Done = 0xFFFF,
		}


		static public async Task Handler(IRSocketProtocol sink, PipeReader pipereader, CancellationToken cancel, string name = null)
		{
			name = name ?? "Client";
			var state = States.Length;

			while (!cancel.IsCancellationRequested)
			{
				Operation($"{name} Reading Pipe");
				var read = await pipereader.ReadAsync(cancel);
				var buffer = read.Buffer;
				if (buffer.IsEmpty && read.IsCompleted) { Operation($"{name} Reading Complete"); break; } else { Operation($"{name} Got Buffer [{buffer.Length}]"); }

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
								if (TryReadByte(buffer, ref position, out var remaining)) { Decode($" [{remaining:X2}] '{Encoding.ASCII.GetString(new[] { remaining })}'", BufferLength: buffer.Length, name: name); break; }
								else { goto Break; } //Break a loop from within a switch. This is the recommended way to do this in C# if you don't want to allocate a looping flag.
							case States.Length: if (TryReadInt32BigEndian(buffer, ref position, out var frame)) { (length, isEndOfMessage) = MessageFrame(frame); state = States.Stream; Decode($" [{length}, {isEndOfMessage:5}]", state, buffer.Length, name: name); break; } else { goto Break; }
							case States.Stream: if (TryReadInt32BigEndian(buffer, ref position, out stream)) { state = States.Flags; Decode($" [0x{stream:X8}]", state, buffer.Length, name: name); break; } else { goto Break; }
							case States.Flags:
								//if (TryReadByte(buffer, ref position, out flags)) { state = States.Remaining; Decode($" [{flags}]", state, buffer.Length); break; } else { goto Break; }
								if (TryReadUInt16BigEndian(buffer, ref position, out var _flags))
								{
									state = States.TypeDispatch;
									type = Header.MakeType(_flags);
									flags = Header.MakeFlags(_flags);
									Decode($" [{type} {(Header.HasIgnore2(flags) ? "Ignore" : "NoIgnore")} {(Header.HasMetadata2(flags) ? "Metadata" : "NoMetadata")} Flags ({flags:X3})]", state, buffer.Length, name: name);
									goto case States.TypeDispatch;
								}
								else { goto Break; }
							case States.TypeDispatch:
								switch (type)
								{
									case Types.Payload: state = States.Payload; break;
									default: throw new InvalidOperationException($"Handler State {state} Invalid Dispatch Type {type}!");
								}
								break;

							case States.Payload:
								//TODO !complete & !next fails.
								if (Payload.HasMetadata(flags)) goto case States.PayloadMetadata;
								if (Payload.HasNext(flags)) goto case States.PayloadData;

								//TODO verify with protocol.
								//OnPayload(sink, new Payload(stream, Span<byte>.Empty, follows: Payload.HasFollows(flags), complete: Payload.HasComplete(flags), next: Payload.HasNext(flags)));
								break;
							case States.PayloadMetadata:
								break;
							case States.PayloadData:
								//TODO Allow zero byte payload
								if (TryReadAll(buffer, ref position, out var data))
								{
									//OnPayload(sink, new Payload(stream, data, follows: Payload.HasFollows(flags), complete: Payload.HasComplete(flags), next: Payload.HasNext(flags)));
									goto case States.Done;
								}
								break;
							case States.Done: state = States.Length; break;		//TODO isEOM handling...
							default: throw new InvalidOperationException($"State Machine Overflow at {state}!");
						}
						buffer = buffer.Slice(position);    //TODO goto case
					}
					Break:
					Operation(buffer.IsEmpty ? $"{name} Recycling with Empty Buffer" : $"{name} Recycling with Partial Buffer", BufferLength: buffer.Length);
					pipereader.AdvanceTo(position, buffer.End);
				}
			}
			Operation($"{name} Completing Pipe");
			pipereader.Complete();
		}

		//static private States PayloadHandler(IRSocketProtocol sink, ushort flags, ReadOnlySequence<byte> buffer)
		//{
		//	States state;
		//	var length = buffer.Length;
		//	var reader = new SequenceReader<byte>(buffer);

		//	if (Payload.HasMetadata(flags))
		//	{
		//	}
		//	else
		//	{

		//	}




		//	switch (state)
		//	{
		//		case States.Payload:
		//			//TODO !complete & !next fails.
		//			if (Payload.HasMetadata(flags)) goto case States.PayloadMetadata;
		//			if (Payload.HasNext(flags)) goto case States.PayloadData;

		//			//TODO verify with protocol.
		//			OnPayload(sink, new Payload(stream, Span<byte>.Empty, follows: Payload.HasFollows(flags), complete: Payload.HasComplete(flags), next: Payload.HasNext(flags)));
		//			break;
		//		case States.PayloadMetadata:
		//			break;
		//		case States.PayloadData:
		//			//TODO Allow zero byte payload
		//			if (TryReadAll(buffer, ref position, out var data))
		//			{
		//				OnPayload(sink, new Payload(stream, data, follows: Payload.HasFollows(flags), complete: Payload.HasComplete(flags), next: Payload.HasNext(flags)));
		//				goto case States.Done;
		//			}
		//			break;
		//	}
		//}
		*/


		//internal ref struct BufferReader
		//{
		//	private ReadOnlySequence<byte> Sequence;
		//	private SequencePosition Position;
		//	private SequencePosition Next;

		//	private ReadOnlySpan<byte> Current;
		//	private int Index;
		//	public bool End { get; private set; }

		//	//private SequencePosition _nextSequencePosition;

		//	//private int _consumedBytes;

		//	//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		//	public BufferReader(in ReadOnlySequence<byte> buffer)
		//	{
		//		Sequence = buffer;
		//		Position = Sequence.Start;
		//		Index = 0;
		//		//_consumedBytes = 0;
		//		Next = Position;

		//		if (Sequence.TryGet(ref Next, out var memory, true))
		//		{
		//			End = false;
		//			Current = memory.Span;
		//			//if (Current.Length == 0) { MoveNext(); }
		//		}
		//		else
		//		{
		//			End = true;
		//			Current = default;
		//		}
		//	}

		//	//bool TryReadInt32BigEndian(in ReadOnlySequence<byte> buffer, out int value)
		//	//{
		//	//	var position = buffer.Start;
		//	//	//while (position < buffer.End)
		//	//	//{
		//	//	buffer.TryGet(ref position, out var memory, advance: false);
		//	//	var frame = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(memory.Span);
		//	//	position = buffer.GetPosition(sizeof(Int32), position);
		//	//	await socket.SendAsync(buffer.Slice(position, frame), _webSocketMessageType);
		//	//	position = buffer.GetPosition(frame, position);
		//	//	//}


		//	//	/// <summary>
		//	//	/// Converts the <see cref="ReadOnlySequence{T}"/> to an array
		//	//	/// </summary>
		//	//	public static T[] ToArray<T>(in this ReadOnlySequence<T> sequence)
		//	//	{
		//	//		var array = new T[sequence.Length];
		//	//		sequence.CopyTo(array);
		//	//		return array;
		//	//	}
		//	//}

		//	////[MethodImpl(MethodImplOptions.NoInlining)]
		//	//private void MoveNext()
		//	//{
		//	//	var previous = _nextSequencePosition;
		//	//	while (Sequence.TryGet(ref _nextSequencePosition, out var memory, true))
		//	//	{
		//	//		_currentSequencePosition = previous;
		//	//		Current = memory.Span; Index = 0;
		//	//		if (Current.Length > 0) { return; }
		//	//	}
		//	//	_end = true;
		//	//}
		//}


		//static bool TryReadAll(ReadOnlySequence<byte> buffer, ref SequencePosition position, out byte[] values)
		//{
		//	buffer = buffer.Slice(position);
		//	if (buffer.IsEmpty) { values = Span<byte>.Empty.ToArray(); return true; }	//TODO garbage.
		//	if (buffer.TryGet(ref position, out var memory)) { values = memory.Span.ToArray(); position = buffer.GetPosition(values.Length); return true; }
		//	else { throw new InvalidOperationException(); }    //This is impossible, since we had at least one byte...
		//}

		//static bool TryReadByte(ReadOnlySequence<byte> buffer, ref SequencePosition position, out Byte value)
		//{
		//	const int SIZEOF = sizeof(Byte);
		//	buffer = buffer.Slice(position);
		//	if (buffer.IsEmpty) { value = 0; return false; }        //TODO Look for Buffer.Length checks - probably bad since it would have to traverse the list.
		//	if (buffer.TryGet(ref position, out var memory)) { value = memory.Span[0]; position = buffer.GetPosition(SIZEOF); return true; }
		//	else { throw new InvalidOperationException(); }    //This is impossible, since we had at least one byte...
		//}

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

		//static bool TryReadUInt16BigEndian(ReadOnlySequence<byte> buffer, ref SequencePosition position, out UInt16 value)
		//{
		//	const int SIZEOF = sizeof(UInt16);
		//	buffer = buffer.Slice(position, SIZEOF);

		//	if (!buffer.TryGet(ref position, out var memory) || !BinaryPrimitives.TryReadUInt16BigEndian(memory.Span, out value))
		//	{
		//		if (buffer.Length < SIZEOF) { value = 0; return false; }

		//		int result = 0;
		//		foreach (var subbuffer in buffer)
		//		{
		//			for (int index = 0; index < subbuffer.Length; index++)
		//			{
		//				result = (result << 8 | subbuffer.Span[index]);
		//			}
		//		}
		//		value = (UInt16)result;
		//	}
		//	position = buffer.GetPosition(SIZEOF);
		//	return true;
		//}
	}
}
