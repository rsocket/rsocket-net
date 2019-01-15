using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Buffers
{
	static public class ReadOnlySequenceFromNetCore3
	{
		//FROM https://github.com/dotnet/corefx/blob/master/src/System.Memory/src/System/Buffers/ReadOnlySequence.cs

		internal static class ReadOnlySequence
		{
			public const int FlagBitMask = 1 << 31;
			public const int IndexBitMask = ~FlagBitMask;
		}

		/// <summary>
		/// Helper to efficiently prepare the <see cref="SequenceReader{T}"/>
		/// </summary>
		/// <param name="first">The first span in the sequence.</param>
		/// <param name="next">The next position.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static internal void GetFirstSpan<T>(this ReadOnlySequence<T> sequence, out ReadOnlySpan<T> first, out SequencePosition next)
		{
			first = default;
			next = default;
			SequencePosition start = sequence.Start;
			int startIndex = start.GetInteger();
			object startObject = start.GetObject();

			if (startObject != null)
			{
				SequencePosition end = sequence.End;
				int endIndex = end.GetInteger();
				bool hasMultipleSegments = startObject != end.GetObject();

				if (startIndex >= 0)
				{
					if (endIndex >= 0)
					{
						// Positive start and end index == ReadOnlySequenceSegment<T>
						ReadOnlySequenceSegment<T> segment = (ReadOnlySequenceSegment<T>)startObject;
						next = new SequencePosition(segment.Next, 0);
						first = segment.Memory.Span;
						if (hasMultipleSegments)
						{
							first = first.Slice(startIndex);
						}
						else
						{
							first = first.Slice(startIndex, endIndex - startIndex);
						}
					}
					else
					{
						// Positive start and negative end index == T[]
						if (hasMultipleSegments)
							ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();

						first = new ReadOnlySpan<T>((T[])startObject, startIndex, (endIndex & ReadOnlySequence.IndexBitMask) - startIndex);
					}
				}
				else
				{
					first = GetFirstSpanSlow<T>(startObject, startIndex, endIndex, hasMultipleSegments);
				}
			}
		}


		//FROM https://github.com/dotnet/corefxlab/blob/master/src/System.Buffers.ReaderWriter/System/Buffers/Reader/BufferReader.cs

		private const int FlagBitMask = 1 << 31;
		private const int IndexBitMask = ~FlagBitMask;

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static ReadOnlySpan<T> GetFirstSpanSlow<T>(object startObject, int startIndex, int endIndex, bool isMultiSegment)
		{
			Debug.Assert(startIndex < 0 || endIndex < 0);
			if (isMultiSegment)
				ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();

			// The type == char check here is redundant. However, we still have it to allow
			// the JIT to see when that the code is unreachable and eliminate it.
			// A == 1 && B == 1 means SequenceType.String
			if (typeof(T) == typeof(char) && endIndex < 0)
			{
				var memory = (ReadOnlyMemory<T>)(object)((string)startObject).AsMemory();

				// No need to remove the FlagBitMask since (endIndex - startIndex) == (endIndex & ReadOnlySequence.IndexBitMask) - (startIndex & ReadOnlySequence.IndexBitMask)
				return memory.Span.Slice(startIndex & IndexBitMask, endIndex - startIndex);
			}
			else // endIndex >= 0, A == 1 && B == 0 means SequenceType.MemoryManager
			{
				startIndex &= IndexBitMask;
				return ((MemoryManager<T>)startObject).Memory.Span.Slice(startIndex, endIndex - startIndex);
			}
		}
	}
}