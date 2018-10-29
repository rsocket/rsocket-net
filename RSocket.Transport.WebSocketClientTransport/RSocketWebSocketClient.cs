using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Pipelines;

namespace RSocket.Transport
{
	using Microsoft.AspNetCore.Http.Connections.Client;
	using Microsoft.AspNetCore.Http.Connections.Client.Internal;

	public class RSocketWebSocketClient : IRSocketTransport
	{
		public Uri Url { get; private set; }
		//ClientWebSocket Client = new ClientWebSocket();
		WebSocketsTransport Transport;

		public PipeReader Input => Transport.Input;
		public PipeWriter Output => Transport.Output;
		bool IRSocketTransport.UseLength => false;

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
		}

		public async Task ConnectAsync()
		{
			await Transport.StartAsync(Url, Microsoft.AspNetCore.Connections.TransferFormat.Binary);

			//TODO Cancellation
		}
	}


	//public class WebSocketClientTransport
	//{
	//	ClientWebSocket Client = new ClientWebSocket();

	//	public WebSocketClientTransport()
	//	{
	//	}


	//	public async Task Test()
	//	{
	//		var cancel = new CancellationTokenSource();
	//		await Client.ConnectAsync(new Uri("ws://rsocket-demo.herokuapp.com/ws"), cancel.Token);
	//		Console.WriteLine("Connected");

	//		//TODO This actually might be better as a factory taking a Transport...
	//		IObservable<string> stream = Client.RequestStream("peace");
	//	}
	//}
}
