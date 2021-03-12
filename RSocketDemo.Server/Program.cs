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
			//Task tttt = TastTest(act);
			//await Test();

			IPAddress iP = IPAddress.Parse("127.0.0.1");
			IPEndPoint iPEndPoint = new IPEndPoint(iP, 8888);
			//Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			SocketTransportFactory socketTransportFactory = new SocketTransportFactory();
			RSocketHost host = new RSocketHost(socketTransportFactory, iPEndPoint);
			var ttt = host.ExecuteAsync(CancellationToken.None);

			Console.ReadKey();
		}


		static async Task RequestStreamTest()
		{
			int initialRequest = 2;
			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);
			await _client.RequestStream(subscriber, "data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			foreach (var item in subscriber.MsgList)
			{
				Console.WriteLine(item);
			}

			Console.ReadKey();
		}

		static async Task RequestChannelTest()
		{
			var source = Observable.Range(1, 15).ToAsyncEnumerable().Select(a => ($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence()));

			int initialRequest = 2000;
			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);
			await _client.RequestChannel(subscriber, source, "data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			foreach (var item in subscriber.MsgList)
			{
				Console.WriteLine($"服务端消息-{item}");
			}

			Console.ReadKey();
		}

		static async Task Test()
		{

			Console.ReadKey();
		}
	}
}
