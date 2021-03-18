using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RSocket
{
	public static class RSocketExtension
	{
		public static async Task<string> RequestResponse(this RSocket rsocket, string data, string metadata = default)
		{
			data = data ?? string.Empty;
			metadata = metadata ?? string.Empty;
			var payload = await rsocket.RequestResponse(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
			string str = Encoding.UTF8.GetString(payload.Data.ToArray());
			return str;
		}
		public static IAsyncEnumerable<string> RequestStream(this RSocket rsocket, string data, string metadata = default)
		{
			data = data ?? string.Empty;
			metadata = metadata ?? string.Empty;
			var payloads = rsocket.RequestStream(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)), int.MaxValue);
			return payloads.ToAsyncEnumerable().Select(a => Encoding.UTF8.GetString(a.Data.ToArray()));
		}

		public static IAsyncEnumerable<string> RequestChannel(this RSocket rsocket, IObservable<string> inputs, string data = default, string metadata = default)
		{
			throw new NotImplementedException();
			//data = data ?? string.Empty;
			//metadata = metadata ?? string.Empty;
			//var payloads = rsocket.RequestChannel(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)), );
			//return payloads.ToAsyncEnumerable().Select(a => Encoding.UTF8.GetString(a.Data.ToArray()));

		}
	}
}
