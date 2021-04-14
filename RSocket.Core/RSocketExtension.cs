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
		public static async Task RequestFireAndForget(this RSocket socket, string data, string metadata = default)
		{
			await socket.RequestFireAndForget(Helpers.StringToByteSequence(data), Helpers.StringToByteSequence(metadata));
		}

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


		internal static async Task OutputSync(this RSocket socket, Func<System.IO.Pipelines.PipeWriter, Task> func)
		{
			//lock (socket.Transport.Output)
			//{
			//	func(socket.Transport.Output).Wait();
			//	return;
			//}

			await socket.OutputSyncLock.WaitAsync();
			try
			{
				/* ps: PipeWriter is not a thread-safe object. */
				await func(socket.Transport.Output);
			}
			finally
			{
				socket.OutputSyncLock.Release();
			}
		}
		internal static Task SendRequestFireAndForget(this RSocket socket, int streamId, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false)
		{
			return socket.OutputSync(output =>
			{
				return new RSocketProtocol.RequestFireAndForget(streamId, data, metadata, initialRequest, follows).WriteFlush(output, data, metadata);
			});
		}
		internal static Task SendRequestResponse(this RSocket socket, int streamId, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false)
		{
			return socket.OutputSync(output =>
			{
				return new RSocketProtocol.RequestResponse(streamId, data, metadata, initialRequest, follows).WriteFlush(output, data, metadata);
			});
		}
		internal static Task SendRequestStream(this RSocket socket, int streamId, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false)
		{
			return socket.OutputSync(output =>
			{
				return new RSocketProtocol.RequestStream(streamId, data, metadata, initialRequest, follows).WriteFlush(output, data, metadata);
			});
		}
		internal static Task SendRequestChannel(this RSocket socket, int streamId, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, Int32 initialRequest = 0, bool follows = false, bool complete = false)
		{
			return socket.OutputSync(output =>
			{
				return new RSocketProtocol.RequestChannel(streamId, data, metadata, initialRequest, follows, complete).WriteFlush(output, data, metadata);
			});
		}
		internal static Task SendPayload(this RSocket socket, int streamId, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default, bool follows = false, bool complete = false, bool next = false)
		{
			return socket.OutputSync(output =>
			   {
				   return new RSocketProtocol.Payload(streamId, data, metadata, follows: follows, complete: complete, next: next).WriteFlush(output, data, metadata);
			   });
		}
		internal static Task SendError(this RSocket socket, int streamId, ErrorCodes errorCode, string errorText, bool throwIfExceptionOccurred = true)
		{
			var errorData = default(ReadOnlySequence<byte>);

			if (errorText != null)
			{
				errorData = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(errorText));
			}
			return SendError(socket, streamId, errorCode, errorData, throwIfExceptionOccurred);
		}
		internal static Task SendError(this RSocket socket, int streamId, ErrorCodes errorCode, ReadOnlySequence<byte> errorData, bool throwIfExceptionOccurred = true)
		{
			try
			{
				return socket.OutputSync(output =>
				{
					return new RSocketProtocol.Error(errorCode, streamId, errorData).WriteFlush(output, errorData);
				});
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
			return socket.OutputSync(output =>
			{
				var cancel = new Cancel(streamId);
				return cancel.WriteFlush(output);
			});
		}
		internal static Task SendRequestN(this RSocket socket, int streamId, int n)
		{
			return socket.OutputSync(output =>
			{
				var requestne = new RequestN(streamId, default(ReadOnlySequence<byte>), initialRequest: n);
				return requestne.WriteFlush(output);
			});
		}
		internal static Task SendKeepAlive(this RSocket socket, int lastReceivedPosition, bool respond)
		{
			return socket.OutputSync(output =>
			{
				RSocketProtocol.KeepAlive keepAlive = new RSocketProtocol.KeepAlive(lastReceivedPosition, respond);
				return keepAlive.WriteFlush(output);
			});
		}
		internal static Task SendSetup(this RSocket socket, TimeSpan keepalive, TimeSpan lifetime, string metadataMimeType = null, string dataMimeType = null, byte[] resumeToken = default, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		{
			return socket.OutputSync(output =>
			{
				return new RSocketProtocol.Setup(keepalive, lifetime, metadataMimeType: metadataMimeType, dataMimeType: dataMimeType, resumeToken: resumeToken, data: data, metadata: metadata).WriteFlush(output, data: data, metadata: metadata);
			});
		}
	}
}
