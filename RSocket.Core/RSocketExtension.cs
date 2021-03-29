using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RSocket.RSocketProtocol;

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

		internal static Task SendPayload(this RSocket socket, Payload payload, int streamId, bool complete = false, bool next = false)
		{
			return new RSocketProtocol.Payload(streamId, payload.Data, payload.Metadata, complete: complete, next: next).WriteFlush(socket.Transport.Output, payload.Data, payload.Metadata);
		}
		internal static Task SendError(this RSocket socket, ErrorCodes errorCode, int streamId, string errorText, bool throwIfExceptionOccurred = true)
		{
			var errorData = default(ReadOnlySequence<byte>);

			if (errorText != null)
			{
				errorData = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(errorText));
			}
			return SendError(socket, errorCode, streamId, errorData, throwIfExceptionOccurred);
		}
		internal static Task SendError(this RSocket socket, ErrorCodes errorCode, int streamId, ReadOnlySequence<byte> errorData, bool throwIfExceptionOccurred = true)
		{
			try
			{
				return new RSocketProtocol.Error(errorCode, streamId, errorData).WriteFlush(socket.Transport.Output, errorData);
			}
			catch
			{
				if (throwIfExceptionOccurred)
					throw;

				return Task.CompletedTask;
			}
		}
		internal static Task SendCancel(this RSocket socket, int streamId = 0)
		{
			var cancel = new Cancel(streamId);
			return cancel.WriteFlush(socket.Transport.Output);
		}
		internal static Task SendRequestN(this RSocket socket, int streamId, int n)
		{
			var requestne = new RequestN(streamId, default(ReadOnlySequence<byte>), initialRequest: n);
			return requestne.WriteFlush(socket.Transport.Output);
		}
		internal static Task SendKeepAlive(this RSocket socket, int lastReceivedPosition, bool respond)
		{
			RSocketProtocol.KeepAlive keepAlive = new RSocketProtocol.KeepAlive(lastReceivedPosition, respond);
			return keepAlive.WriteFlush(socket.Transport.Output);
		}
	}
}
