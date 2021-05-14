using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets
{
	internal static class SocketExtension
	{
		public static ValueTask SendAsync(this Socket socket, ReadOnlySequence<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default)
		{
#if NETCOREAPP3_0
            if (buffer.IsSingleSegment)
            {
                return socket.SendAsync(buffer.First, webSocketMessageType, endOfMessage: true, cancellationToken);
            }
            else { return SendMultiSegmentAsync(socket, buffer, socketFlags, cancellationToken); }
#else
			if (buffer.IsSingleSegment)
			{
				var isArray = MemoryMarshal.TryGetArray(buffer.First, out var segment);
				Debug.Assert(isArray);
				return new ValueTask(socket.SendAsync(segment, socketFlags));       //TODO Cancellation?
			}
			else { return SendMultiSegmentAsync(socket, buffer, socketFlags, cancellationToken); }
#endif
		}

		static async ValueTask SendMultiSegmentAsync(Socket socket, ReadOnlySequence<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default)
		{
#if NETCOREAPP3_0
			var position = buffer.Start;
			buffer.TryGet(ref position, out var prevSegment);
			while (buffer.TryGet(ref position, out var segment))
			{
				await socket.SendAsync(prevSegment, socketFlags);
				prevSegment = segment;
			}
			await socket.SendAsync(prevSegment, socketFlags);
#else
			var position = buffer.Start;
			buffer.TryGet(ref position, out var prevSegment);
			while (buffer.TryGet(ref position, out var segment))
			{
				var isArray = MemoryMarshal.TryGetArray(prevSegment, out var arraySegment);
				Debug.Assert(isArray);
				await socket.SendAsync(arraySegment, socketFlags);
				prevSegment = segment;
			}
			var isArrayEnd = MemoryMarshal.TryGetArray(prevSegment, out var arraySegmentEnd);
			Debug.Assert(isArrayEnd);
			await socket.SendAsync(arraySegmentEnd, socketFlags);
#endif
		}
	}
}
