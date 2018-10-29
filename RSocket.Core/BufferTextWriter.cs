using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RSocket
{
	public sealed class BufferTextWriter : System.IO.TextWriter
	{
		public BufferWriter BufferWriter { get; set; }
		//public Encoding _Encoding;
		//private readonly Encoder Encoder;
		//private readonly int MaximumBytesPerChar;
		private readonly int SingleByteThreshold = 0;
		public override Encoding Encoding => BufferWriter.Encoding;

		public BufferTextWriter(IBufferWriter<byte> bufferWriter, Encoding encoding = null)
		{
			BufferWriter = new BufferWriter(bufferWriter, encoding);

			//_Encoding =  this.BufferWriter.Encoding;
			SingleByteThreshold = encoding == null ? 127 : -1;
			//MaximumBytesPerChar = _Encoding.GetMaxByteCount(1);
			//Encoder = _Encoding.GetEncoder();
		}

		public override void Write(string text) => BufferWriter.Write(text);
		public override void Write(char[] buffer, int index, int count) => BufferWriter.Write(buffer, index, count);
		public override void Write(char[] buffer) => BufferWriter.Write(buffer);
		public override void Write(char value) { if (value < SingleByteThreshold) { BufferWriter.Write((byte)value); } else { BufferWriter.Write(value); } }


		[ThreadStatic]
		private static BufferTextWriter Instance;
#if DEBUG
		private bool InUse => BufferWriter.InUse;
#endif

		public static BufferTextWriter Get(IBufferWriter<byte> bufferWriter)
		{
			var writer = Instance ?? new BufferTextWriter(null);		//Special initialization to track prior use.
			Instance = null;     // Taken off the thread static
#if DEBUG
			if (writer.InUse) { throw new InvalidOperationException($"The {nameof(BufferTextWriter)} wasn't returned!"); }
#endif
			writer.BufferWriter.Reset(bufferWriter);
			return writer;
		}

		public static void Return(BufferTextWriter writer)
		{
			writer.BufferWriter.Reset();
			Instance = writer;
		}

		public override void Flush() => BufferWriter.Flush();

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing) { Flush(); }
		}
	}
}