// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Buffers
{
	public static partial class SequenceReaderExtensions
	{
		/// <summary>
		/// Reads an <see cref="Int16"/> as little endian.
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="Int16"/>.</returns>
		public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out ushort value)
		{
			if (BitConverter.IsLittleEndian)
			{
				return reader.TryRead(out value);
			}

			return TryReadReverseEndianness(ref reader, out value);
		}

		/// <summary>
		/// Reads an <see cref="Int16"/> as big endian.
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="Int16"/>.</returns>
		public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out ushort value)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return reader.TryRead(out value);
			}

			return TryReadReverseEndianness(ref reader, out value);
		}

		private static bool TryReadReverseEndianness(ref SequenceReader<byte> reader, out ushort value)
		{
			if (reader.TryRead(out value))
			{
				value = BinaryPrimitives.ReverseEndianness(value);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Reads an <see cref="Int32"/> as little endian.
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="Int32"/>.</returns>
		public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out uint value)
		{
			if (BitConverter.IsLittleEndian)
			{
				return reader.TryRead(out value);
			}

			return TryReadReverseEndianness(ref reader, out value);
		}

		/// <summary>
		/// Reads an <see cref="Int32"/> as big endian.
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="Int32"/>.</returns>
		public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out uint value)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return reader.TryRead(out value);
			}

			return TryReadReverseEndianness(ref reader, out value);
		}

		private static bool TryReadReverseEndianness(ref SequenceReader<byte> reader, out uint value)
		{
			if (reader.TryRead(out value))
			{
				value = BinaryPrimitives.ReverseEndianness(value);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Reads an <see cref="Int64"/> as little endian.
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="Int64"/>.</returns>
		public static bool TryReadLittleEndian(ref this SequenceReader<byte> reader, out ulong value)
		{
			if (BitConverter.IsLittleEndian)
			{
				return reader.TryRead(out value);
			}

			return TryReadReverseEndianness(ref reader, out value);
		}

		/// <summary>
		/// Reads an <see cref="Int64"/> as big endian.
		/// </summary>
		/// <returns>False if there wasn't enough data for an <see cref="Int64"/>.</returns>
		public static bool TryReadBigEndian(ref this SequenceReader<byte> reader, out ulong value)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return reader.TryRead(out value);
			}

			return TryReadReverseEndianness(ref reader, out value);
		}

		private static bool TryReadReverseEndianness(ref SequenceReader<byte> reader, out ulong value)
		{
			if (reader.TryRead(out value))
			{
				value = BinaryPrimitives.ReverseEndianness(value);
				return true;
			}

			return false;
		}
	}
}