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
			await Test();

			IPAddress iP = IPAddress.Parse("127.0.0.1");
			IPEndPoint iPEndPoint = new IPEndPoint(iP, 8888);
			//Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			SocketTransportFactory socketTransportFactory = new SocketTransportFactory();
			RSocketHost host = new RSocketHost(socketTransportFactory, iPEndPoint);
			var ttt = host.ExecuteAsync(CancellationToken.None);

			//Thread.Sleep(1000);
			//_server = host._connections.First().Value.Server;

			SocketTransport socketTransport = new SocketTransport("tcp://127.0.0.1:8888/");
			_client = new EchoRSocketClient(socketTransport, new RSocketOptions() { InitialRequestSize = int.MaxValue });
			await _client.ConnectAsync();

			Console.ReadKey();

			//await RequestStreamTest();
			await RequestChannelTest();


			////创建与远程主机的连接
			//serverSocket.Connect(iPEndPoint);


			//var loopback = new LoopbackTransport();
			//var server = new EchoServer(loopback.Beyond);
			//await server.ConnectAsync();
			//SocketTransportFactory x = null;
			//var client = new RSocketClient(new SocketTransport("tcp://localhost:9091/"), new RSocketOptions() { InitialRequestSize = 3 });
			//var client = new RSocketClient(new WebSocketTransport("ws://localhost:9092/"), new RSocketOptions() { InitialRequestSize = 3 });


			IAsyncEnumerable<string> result = null;

			var source = Observable.Range(1, 10).ToAsyncEnumerable().Select(a =>
			{
				//Thread.Sleep(1000);
				return a.ToString();
			});


			IObserver<string> o = null;
			var observable = Observable.Create<string>(observer =>
			{
				o = observer;
				//observer.OnNext(1.ToString());
				return Disposable.Empty;
			});

			source = observable.ToAsyncEnumerable();

			var (data, metadata) = ("1", "2");
			var channelRes = _client.RequestChannel(source, data, metadata);

			var xxx = channelRes.ForEachAsync(a =>
		 {
			 Console.WriteLine($"res -{a}");
		 });

			Console.WriteLine(999);

			int i = 0;
			while (true)
			{
				Console.ReadKey();

				if (i == 2)
				{
					o.OnCompleted();
					break;
				}

				o.OnNext(i++.ToString());
			}

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
			//await _client.RequestChannel(subscriber, source, "data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			foreach (var item in subscriber.MsgList)
			{
				Console.WriteLine($"服务端消息-{item}");
			}

			Console.ReadKey();
		}

		static async Task Test()
		{
			IObserver<int> ob = null;
			var cts = new CancellationTokenSource();

			var source = Observable.Create<int>((o) =>
		 {
			 //cancelTask = Task.FromCanceled(cancel);
			 ob = o;
			 //o.OnNext(1);
			 //ob.OnCompleted();
			 //Console.WriteLine("bbbb");
			 //ob.OnCompleted();

			 //for (int i = 0; i < int.MaxValue; i++)
			 //{
			 // o.OnNext(i);
			 // Thread.Sleep(1000);
			 //}


			 return () =>
		   {
			   Console.WriteLine("disposing");
		   };
		 });


			var t = Task.Run(async () =>
				 {
					 //await a;
					 Console.WriteLine("111111111");
					 Thread.Sleep(1000);
					 ob.OnNext(10000);
					 Console.WriteLine("22222222222");
					 ob.OnNext(20000);
					 //await Task.Delay(1);
					 ob.OnCompleted();

					 Console.WriteLine("3333333");
				 });

			Console.WriteLine((await source.ToAsyncEnumerable().ToListAsync()).Count);

			while (true)
			{
				Console.WriteLine(t.Status);
				Console.ReadKey();
			}

			int i = 0;
			while (true)
			{
				Console.WriteLine("aaaaaaaaaa");
				Console.ReadKey();
				ob.OnNext(i++);
				cts.Cancel();

				ob.OnNext(i++);
				ob.OnCompleted();
			}

			Console.ReadKey();
		}
	}
}
