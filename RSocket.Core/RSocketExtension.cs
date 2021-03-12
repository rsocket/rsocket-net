using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RSocket
{
	public static class RSocketExtension
	{
		public static Task<string> RequestResponse(this RSocket rsocket, string data, string metadata = default) => rsocket.RequestResponse(value => Encoding.UTF8.GetString(value.Data.ToArray()), new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
		public static IAsyncEnumerable<string> RequestStream(this RSocket rsocket, string data, string metadata = default)
		{
			return rsocket.RequestStream(value =>
			{
				return Encoding.UTF8.GetString(value.Data.ToArray());
			}, new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
		}

		public static IAsyncEnumerable<string> RequestChannel(this RSocket rsocket, IAsyncEnumerable<string> inputs, string data = default, string metadata = default) =>
			rsocket.RequestChannel(inputs, input => new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(input)), result => Encoding.UTF8.GetString(result.Data.ToArray()),
				data == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)),
				metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
	}
}
