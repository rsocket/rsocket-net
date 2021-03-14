using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	public struct PayloadContent
	{
		ReadOnlySequence<byte> _data;
		ReadOnlySequence<byte> _metadata;

		public ReadOnlySequence<byte> Data { get { return this._data; } }
		public ReadOnlySequence<byte> Metadata { get { return this._metadata; } }

		public PayloadContent(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)
		{
			this._data = data;
			this._metadata = metadata;
		}
	}
}
