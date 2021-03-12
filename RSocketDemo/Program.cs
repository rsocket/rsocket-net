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
			await _client.RequestChannel(subscriber, source, "data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			foreach (var item in subscriber.MsgList)
			{
				Console.WriteLine($"服务端消息-{item}");
			}

			Console.ReadKey();
		}

		static async Task Test()
		{
			IObserver<int> ob = null;
			Action<IObserver<int>> act = a =>
			{
				ob = a;
			};


			var x = Observable.Range(1, 10).ToAsyncEnumerable();

			//x.ForEachAsync
			var xx = Observable.Range(1, 10).ToAsyncEnumerable();

			var list = await xx.Take(5).ToListAsync();

			list = await xx.Take(5).ToListAsync();

			var cts = new CancellationTokenSource();
			Task cancelTask = Task.Delay(-1, cts.Token);

			//cts.Cancel();

			var source = Observable.Create<int>((o) =>
		 {
			 //cancelTask = Task.FromCanceled(cancel);
			 ob = o;
			 //o.OnNext(1);
			 //ob.OnCompleted();

			 //for (int i = 0; i < int.MaxValue; i++)
			 //{
			 // o.OnNext(i);
			 // Thread.Sleep(1000);
			 //}


			 return () =>
		   {
			   Console.WriteLine("disposing");
			   //cts.Cancel();
		   };

			 //return Disposable.Empty;
		 });

			var iiii = source.Subscribe((a) =>
			   {
				   Console.WriteLine(a);
			   }, () =>
			   {
				   Console.WriteLine("over");
			   });

			//var iiiiiii = source.Subscribe((a) =>
			//{
			//	Console.WriteLine(a + 100);
			//}, () =>
			//{
			//	Console.WriteLine("over1111111");
			//});

			var e = source.ToAsyncEnumerable().GetAsyncEnumerator();


			//var tttttttt = Task.Run(async () =>
			//{
			//	Console.WriteLine("start");
			//	try
			//	{
			//		await e.MoveNextAsync();
			//	}
			//	catch
			//	{
			//		Console.WriteLine("ex");
			//	}
			//	Console.WriteLine("over");
			//});

			ob.OnNext(1);
			Console.ReadKey();
			ob.OnNext(2);
			ob.OnCompleted();
			iiii.Dispose();

			ob.OnNext(3);

			//await e.DisposeAsync();
			//tttttttt.Dispose();
			//Console.ReadKey();
			ob.OnCompleted();
			Console.WriteLine(await e.MoveNextAsync());
			Console.ReadKey();
			await foreach (var item in source.ToAsyncEnumerable())
			{
				Console.WriteLine(item);
			}
			Console.Read();
			var tttttt = Task.Run(async () =>
			 {
				 try
				 {
					 await source.ToAsyncEnumerable().ForEachAsync(a =>
					   {
						   Console.WriteLine(a);
					   }, cts.Token);
				 }
				 catch (Exception ex)
				 {
					 Console.WriteLine("ex");
				 }

				 //var e = source.ToAsyncEnumerable().GetAsyncEnumerator();

				 //Console.WriteLine("start");
				 //while (await e.MoveNextAsync())
				 //{
				 // Console.WriteLine(e.Current);
				 //}
				 Console.WriteLine("over");
			 });

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

			//source = Observable.Range(1, 10);
			//cts.Cancel();



			await source.ToAsyncEnumerable().ForEachAsync(a =>
			  {
				  //Thread.Sleep(1000);

				  //if (a > 2)
				  //{
				  //	cts.Cancel();
				  //}

				  Console.WriteLine(a);

			  });

			var ttttt = await Task.Run(async delegate
			 {
				 Console.WriteLine(1111111);
				 await cancelTask;
				 Console.WriteLine(2222222);
				 return 42;
			 });

			Console.ReadKey();
			ob.OnNext(2);
			cts.Cancel();
			ob.OnNext(3);
			Console.ReadKey();

			var xxxxxx = source.ToAsyncEnumerable();

			//source.LastOrDefaultAsync();
			var a = await source.LastOrDefaultAsync();

			var enumerator = x.GetAsyncEnumerator();

			var ss = source.ToAsyncEnumerable();

			var t = ss.ForEachAwaitAsync(async a =>
			{
				Console.WriteLine(a);

				var count = a;
				while (count > 0)
				{
					var next = await enumerator.MoveNextAsync();

					if (next)
					{
						Console.WriteLine(enumerator.Current);
						count--;
						continue;
					}
					else
					{
						//没有数据了
						Console.WriteLine("没有数据了");
						ob.OnCompleted();
						break;
					}
				}
			});

			Console.WriteLine(5555);
			while (true)
			{
				Console.WriteLine("ob.OnNext(5);");
				ob.OnNext(5);
				Console.WriteLine("ob.OnNext(5); over");
				Console.ReadKey();
			}

			Console.ReadKey();
		}

		static async Task TastTest(Action<IObserver<int>> act)
		{
			var observable = Observable.Create<int>(observer =>
			{
				//var id = StreamDispatch(receiver);
				//Subscriber(observer).ConfigureAwait(false);

				act(observer);
				observer.OnError(new Exception());
				return Disposable.Empty;
			});

			//return observable.ToTask();

			//await observable.LastOrDefaultAsync();

			var result = await observable.ToAsyncEnumerable().ToListAsync();

			//return result;

			//await result.ForEachAsync(a =>
			//{
			//	Console.WriteLine(a);
			//});

			//try
			//{
			//	await foreach (var item in result)
			//	{
			//		Console.WriteLine(item);
			//	}
			//}
			//catch (Exception ex)
			//{
			//	//receiver.OnError(ex);
			//	throw;
			//}
		}

		static void Bind()
		{
			//if (_listenSocket != null)
			//{
			//	throw new InvalidOperationException(SocketsStrings.TransportAlreadyBound);
			//}

			IPAddress iP = IPAddress.Parse("127.0.0.1");
			IPEndPoint EndPoint = new IPEndPoint(iP, 8888);

			Socket listenSocket;

			listenSocket = new Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			BindSocket();

			void BindSocket()
			{
				try
				{
					listenSocket.Bind(EndPoint);
				}
				catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
				{
					throw new Exception(e.Message, e);
				}
			}

			//EndPoint = listenSocket.LocalEndPoint;

			listenSocket.Listen(100);

			//_listenSocket = listenSocket;
		}
	}
}
