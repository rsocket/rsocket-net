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
			Console.WriteLine("client");
			Console.ReadKey();

			while (true)
			{
				SocketTransport socketTransport = new SocketTransport("tcp://127.0.0.1:8888/");
				_client = new EchoRSocketClient(socketTransport, new RSocketOptions() { InitialRequestSize = int.MaxValue });
				await _client.ConnectAsync();

				await RequestFireAndForgetTest();

				//await RequestResponseTest();

				//await RequestStreamTest();
				//await RequestStreamTest1();

				//await RequestChannelTest();
				//await RequestChannelTest1();

				Console.ReadKey();
			}
		}

		static async Task RequestFireAndForgetTest()
		{
			await _client.RequestFireAndForget("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence());

			Console.WriteLine($"RequestFireAndForget 结束");
			Console.ReadKey();
		}

		static async Task RequestResponseTest()
		{
			var result = await _client.RequestResponse("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence());

			Console.WriteLine($"收到服务端消息-{result.Data.ConvertToString()}");

			Console.WriteLine($"RequestResponse 结束");
			Console.ReadKey();
		}

		static async Task RequestStreamTest()
		{
			int initialRequest = int.MaxValue;
			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);
			var result = _client.RequestStream("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			await foreach (var item in result.ToAsyncEnumerable())
			{
				Console.WriteLine($"收到服务端消息-{item.Data.ConvertToString()}");
			}

			Console.WriteLine($"RequestStream 结束");
			Console.ReadKey();
		}
		static async Task RequestStreamTest1()
		{
			int initialRequest = 2;
			//int initialRequest = int.MaxValue;

			var result = _client.RequestStream("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), initialRequest);

			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);
			subscriber.MaxReceives = 5;
			var subscription = result.Subscribe(subscriber);
			ISubscription sub = subscription as ISubscription;
			subscriber.OnSubscribe(sub);

			await subscriber.Block();

			Console.WriteLine($"收到服务端消息条数-{subscriber.MsgList.Count}");

			Console.WriteLine($"RequestStream 结束");
			Console.ReadKey();
		}


		static async Task RequestChannelTest()
		{
			//int initialRequest = 2;
			int initialRequest = int.MaxValue;
			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);

			//var t = Task.Run(() =>
			//{
			//	Thread.Sleep(10000);
			//	Console.WriteLine("subscriber.Subscription.Cancel()");
			//	subscriber.Subscription.Cancel();
			//});

			var result = RequestChannel(10, initialRequest);

			await foreach (var item in result.ToAsyncEnumerable())
			{
				Console.WriteLine($"收到服务端消息-{item.Data.ConvertToString()}");
			}

			Console.WriteLine($"RequestChannel 结束");
			Console.ReadKey();
		}
		static async Task RequestChannelTest1()
		{
			int initialRequest = 2;
			//int initialRequest = int.MaxValue;

			var result = RequestChannel(10, initialRequest);

			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);
			subscriber.MaxReceives = 5;
			var subscription = result.Subscribe(subscriber);
			subscriber.OnSubscribe(subscription);

			await subscriber.Block();

			Console.WriteLine($"收到服务端消息条数-{subscriber.MsgList.Count}");

			Console.WriteLine($"RequestChannel 结束");
			Console.ReadKey();
		}

		static IPublisher<PayloadContent> RequestChannel(int outputs, int initialRequest)
		{
			IObserver<int> ob = null;
			var source = Observable.Create<int>(o =>
			{
				ob = o;
				Task.Run(() =>
				{
					for (int i = 0; i < outputs; i++)
					{
						Thread.Sleep(1000);
						o.OnNext(i);
					}

					o.OnCompleted();
				});

				return () =>
				{
					Console.WriteLine("客户端stream dispose");
				};
			}).Select(a =>
			{
				Console.WriteLine($"生成客户端消息-{a}");
				return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);

			//var t = Task.Run(() =>
			//{
			//	Thread.Sleep(10000);
			//	Console.WriteLine("subscriber.Subscription.Cancel()");
			//	subscriber.Subscription.Cancel();
			//});

			var result = _client.RequestChannel("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), source, initialRequest);

			return result;
		}
	}
}
