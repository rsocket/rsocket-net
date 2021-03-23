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
		public static async Task<string> RequestResponse(this RSocket socket, string data, string metadata = default)
		{
			var payload = await socket.RequestResponse(Helpers.StringToByteSequence(data), Helpers.StringToByteSequence(metadata));
			string str = Encoding.UTF8.GetString(payload.Data.ToArray());
			return str;
		}

		public static IAsyncEnumerable<string> RequestStream(this RSocket socket, string data, string metadata = default)
		{
			var payloads = socket.RequestStream(Helpers.StringToByteSequence(data), Helpers.StringToByteSequence(metadata), int.MaxValue);
			return payloads.ToAsyncEnumerable().Select(a => Encoding.UTF8.GetString(a.Data.ToArray()));
		}

		public static IAsyncEnumerable<string> RequestChannel(this RSocket socket, IAsyncEnumerable<string> inputs, string data = default, string metadata = default)
		{
			IObservable<Payload> source = inputs.Select(a => new Payload(Helpers.StringToByteSequence(a))).ToObservable();
			return socket.RequestChannel(Helpers.StringToByteSequence(data), Helpers.StringToByteSequence(metadata), source, int.MaxValue).ToAsyncEnumerable().Select(a => Encoding.UTF8.GetString(a.Data.ToArray()));
		}
	}
}
