using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Pipelines;

namespace RSocket.Transports
{
	using Microsoft.AspNetCore.Http.Connections.Client;
	using Microsoft.AspNetCore.Http.Connections.Client.Internal;

	public class RSocketWebSocketClient : IRSocketTransport
	{
		public Uri Url { get; private set; }
		WebSocketsTransport Transport;
		WebSocketTransport Transport2;

		public PipeReader Input => Transport.Input;
		public PipeWriter Output => Transport.Output;

		public RSocketWebSocketClient(string url) : this(new Uri(url)) { }
		public RSocketWebSocketClient(Uri url)
		{
			Url = url;
			var logger = new Microsoft.Extensions.Logging.LoggerFactory(new[] { new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider() });

			//TODO Really, we should take the CWSO and invert it into the HTTPCO below - super goofy since they just reverse it later on...
			//new ClientWebSocketOptions().

			Transport = new WebSocketsTransport(new HttpConnectionOptions()
			{
				Url = url,
			}, logger, null);

			Transport2 = new WebSocketTransport(url);
		}

		public async Task StartAsync(CancellationToken cancel = default)
		{
			await Transport.StartAsync(Url, Microsoft.AspNetCore.Connections.TransferFormat.Binary);
			//await Transport2.ConnectAsync(cancel);
		}

		public Task StopAsync() => Task.CompletedTask;
	}
}
