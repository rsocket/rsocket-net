using System;
using System.Threading.Tasks;
using RSocket;
using RSocket.Transport;

namespace RSocketSample
{
	class Program
	{
		static async Task Main(string[] args)
		{
			Console.WriteLine("Connecting...");

			var client = new RSocketClient(new RSocketWebSocketClient("ws://rsocket-demo.herokuapp.com/ws"));
			await client.ConnectAsync();
			Console.ReadKey();
			Console.WriteLine("Requesting Stream...");
			await client.RequestStream("peace", initial: 2);

			//var client = new RSocketClient(new RSocketWebSocketClient("ws://echo.websocket.org/?encoding=text"));
			//await client.Test("Howdy Planet!!");

			Console.ReadKey();
		}
	}
}
