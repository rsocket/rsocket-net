// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Buffers
{
	public static partial class SequenceReaderExtensions
	{
		//[MethodImpl(MethodImplOptions.AggressiveInlining)] static bool TryFail(out string value) { value = default; return false; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] static bool Tried(bool tried, out string value, string from) { value = from; return tried; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] static TResult Undo<T, TResult>(this ref SequenceReader<T> reader, long count, TResult result) where T : unmanaged, IEquatable<T> { reader.Rewind(count); return result; }

		/// <summary>Attempts to read a Span<typeparamref name="T"/> from a sequence.</summary>
		/// <param name="reader">The reader to extract the Span from; the reader will advance.</param>
		/// <param name="destination">The destination Span.</param>
		/// <returns>True if the sequence had enough to fill the span.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static public bool TryRead<T>(this ref SequenceReader<T> reader, Span<T> destination) where T : unmanaged, IEquatable<T> { if (reader.TryCopyTo(destination)) { reader.Advance(destination.Length); return true; } else { return false; } }

		/// <summary>Attempt to read a byte-prefixed string from a sequence.</summary>
		/// <param name="reader">The reader to extract the string from; the reader will advance.</param>
		/// <param name="value">The resulting value.</param>
		/// <param name="encoding">The Encoding of the string bytes. Defaults to ASCII.</param>
		/// <returns>True if the sequence had enough to fill the string.</returns>
		public static bool TryReadPrefix(ref this SequenceReader<byte> reader, out string value, Encoding encoding = default) => reader.TryRead(out byte length) ? TryRead(ref reader, out value, length, encoding) : Undo(ref reader, 1, Tried(false, out value, default));

		//TODO DOCS
		public static unsafe bool TryRead(ref this SequenceReader<byte> reader, out string value, byte length, Encoding encoding = default)
		{
			var backing = stackalloc byte[length];
			return reader.TryRead(new Span<byte>(backing, length)) ? Tried(true, out value, (encoding ?? Encoding.ASCII).GetString(backing, length)) : Tried(false, out value, default);
		}

		//TODO DOCS
		public static unsafe bool TryRead(ref this SequenceReader<byte> reader, out string value, int length, Encoding encoding = default)
		{
			var backing = new byte[length];
			return reader.TryRead(new Span<byte>(backing)) ? Tried(true, out value, (encoding ?? Encoding.UTF8).GetString(backing)) : Tried(false, out value, default);
		}

		/// <summary>
		/// Reads an <see cref="UInt24"/> as big endian;
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="UInt24"/>.</returns>
		public static bool TryReadUInt24BigEndian(ref this SequenceReader<byte> reader, out int value)
		{
			var span = new Span<byte>(new byte[sizeof(UInt32) - 1]);
			if (!reader.TryRead(span)) { value = default; return false; }
			else { value = span[0] << 16 | span[1] << 8 | span[2]; return true; }
		}
	}
}