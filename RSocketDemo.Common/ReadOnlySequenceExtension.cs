using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers;

namespace System.Buffers
{
	public static class ReadOnlySequenceExtension
	{
		public static string ConvertToString(this ReadOnlySequence<byte> data)
		{
			string str = Encoding.UTF8.GetString(data.ToArray());
			return str;
		}
	}
}

namespace System
{
	public static class StringExtension
	{
		public static ReadOnlySequence<byte> ToReadOnlySequence(this string data)
		{
			var ret = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data));
			return ret;
		}
	}
}
