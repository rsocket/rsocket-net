using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using RSocket;
using RSocket.Transports;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Buffers;
using System.Text;
using System.Reactive.Disposables;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;

namespace RSocketDemo
{
	class Program
	{
		static RSocketServer _server;
		static RSocketClient _client;
		static async Task Main(string[] args)
		{
			Console.WriteLine("Run RSocketDemo.Client and RSocketDemo.Server please.");
			Console.ReadKey();
			await Test();

			IPAddress iP = IPAddress.Parse("127.0.0.1");
			IPEndPoint iPEndPoint = new IPEndPoint(iP, 8888);

			SocketTransportFactory socketTransportFactory = new SocketTransportFactory();
			RSocketHost host = new RSocketHost(socketTransportFactory, iPEndPoint, a =>
			{
				return new EchoServer(a);
			});
			var hostTask = host.ExecuteAsync(CancellationToken.None);

			ClientSocketTransport socketTransport = await ClientSocketTransport.Create("tcp://127.0.0.1:8888/");
			_client = new EchoRSocketClient(socketTransport, new RSocketOptions() { InitialRequestSize = int.MaxValue });
			await _client.ConnectAsync();

			Console.ReadKey();
		}

		static async Task Test()
		{
			Console.WriteLine("---------------------");
			Console.ReadKey();
		}
	}
}
