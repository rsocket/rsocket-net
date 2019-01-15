using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket.Transports
{
	using System.Net.Sockets;
	using System.Net.WebSockets;

	internal static partial class RSocketTransportExtensions
	{
		//Framing helper methods. They stay close to the original code as called, but add and remove the framing at the transport-socket boundary.
		public static Memory<byte> GetMemory(this PipeWriter output, out Memory<byte> memoryframe, bool haslength = false) { memoryframe = output.GetMemory(); return memoryframe.Slice(haslength ? 1 : sizeof(int)); }
		public static void Advance(this PipeWriter output, int bytes, bool endOfMessage, in Memory<byte> memoryframe) { System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(memoryframe.Span, RSocketProtocol.MessageFrame(bytes, endOfMessage)); output.Advance(sizeof(int) + bytes); }

		public static ValueTask SendAsync(this WebSocket socket, ReadOnlySequence<byte> buffer, SequencePosition position, WebSocketMessageType webSocketMessageType, CancellationToken cancellationToken = default)
		{
			buffer.TryGet(ref position, out var memory, advance: false);
			var (length, isEndOfMessage) = RSocketProtocol.MessageFrame(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(memory.Span));
			position = buffer.GetPosition(sizeof(int), position);
			return socket.SendAsync(buffer.Slice(position, length), webSocketMessageType, cancellationToken);
		}

		public static ValueTask SendAsync(this Socket socket, ReadOnlySequence<byte> buffer, SequencePosition position, SocketFlags socketFlags, CancellationToken cancellationToken = default)
		{
			buffer.TryGet(ref position, out var memory, advance: false);
			var (length, isEndOfMessage) = RSocketProtocol.MessageFrame(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(memory.Span));
			position = buffer.GetPosition(1, position);
			return socket.SendAsync(buffer.Slice(position, length), socketFlags, cancellationToken);
		}
	}
}
