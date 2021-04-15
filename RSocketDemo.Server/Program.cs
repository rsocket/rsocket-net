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
		static async Task Main(string[] args)
		{
			await Task.CompletedTask;
			IPAddress iP = IPAddress.Parse("127.0.0.1");
			IPEndPoint iPEndPoint = new IPEndPoint(iP, 8888);

			SocketListenerFactory socketListenerFactory = new SocketListenerFactory();
			RSocketHost host = new RSocketHost(socketListenerFactory, iPEndPoint, a =>
			{
				return new RSocketDemoServer(a);
			});
			var task = host.ExecuteAsync(CancellationToken.None);
			Console.WriteLine("server started...");

			while (true)
			{
				string cmd = Console.ReadLine();
				if (cmd == "clear")
				{
					Console.Clear();
					continue;
				}

				if (cmd == "exit")
				{
					Console.Clear();
					break;
				}
			}
		}
	}
}
