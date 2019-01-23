using System;
using System.Buffers;

namespace RSocket
{
	/// <summary>
	/// A stream of items from an RSocket. This is simply an Observer of the protcol's tuples.
	/// </summary>
	public interface IRSocketStream : IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>
	{
	}
}
