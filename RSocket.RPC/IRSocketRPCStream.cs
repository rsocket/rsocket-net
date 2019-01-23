using System;
using System.Buffers;

namespace RSocket.RPC
{
	public interface IRSocketRPCStream : IObserver<(string Service, string Method, ReadOnlySequence<byte> Metadata, ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Tracing)>
	{
	}
}
