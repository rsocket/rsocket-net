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

				//await RequestStreamTest();
				//await RequestChannelTest();
				await RequestChannelCancelTest();

				Console.ReadKey();
			}

		}

		async Task Test()
		{

		}

		static async Task RequestStreamTest()
		{
			int initialRequest = int.MaxValue;
			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);
			await _client.RequestStream(subscriber, "data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			Console.WriteLine(subscriber.MsgList.Count);
			foreach (var item in subscriber.MsgList)
			{
				Console.WriteLine(item);
			}

			Console.ReadKey();
		}


		static async Task RequestChannelTest()
		{
			var source = Observable.Range(1, 5).Select(a =>
		   {
			   //Thread.Sleep(1000);
			   return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
		   }
			);

			//int initialRequest = 2;
			int initialRequest = int.MaxValue;
			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);

			//var t = Task.Run(() =>
			//{
			//	Thread.Sleep(10000);
			//	Console.WriteLine("subscriber.Subscription.Cancel()");
			//	subscriber.Subscription.Cancel();
			//});

			var result = _client.RequestChannel(source, "data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			await foreach (var item in result.ToAsyncEnumerable())
			{
				Console.WriteLine($"服务端消息-{item.Data.ConvertToString()}");
			}

			Console.ReadKey();
		}
		static async Task RequestChannelCancelTest()
		{
			var source = Observable.Range(1, 5).Select(a =>
			{
				//Thread.Sleep(1000);
				return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);

			//int initialRequest = 1;
			int initialRequest = int.MaxValue;
			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);

			var result = _client.RequestChannel(source, "data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			result.Subscribe(subscriber);

			while (true)
			{
				Console.ReadKey();
				Console.WriteLine(subscriber.MsgList.Count);
			}

			//await foreach (var item in result.ToAsyncEnumerable())
			//{
			//	Console.WriteLine($"服务端消息-{item.Data.ConvertToString()}");
			//}

			Console.ReadKey();
		}

		static async Task RequestChannelTest1()
		{
			var source = Observable.Range(1, 5).ToAsyncEnumerable().Select(a =>
			{
				Thread.Sleep(1000);
				return ($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);

			//int initialRequest = 2;
			int initialRequest = int.MaxValue;
			RequestStreamSubscriber subscriber = new RequestStreamSubscriber(initialRequest);

			var t = Task.Run(() =>
			{
				Thread.Sleep(10000);
				Console.WriteLine("subscriber.Subscription.Cancel()");
				subscriber.Subscription.Cancel();
			});

			await _client.RequestChannel(subscriber, source, "data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), subscriber.RequestSize);

			foreach (var item in subscriber.MsgList)
			{
				Console.WriteLine($"服务端消息-{item}");
			}

			Console.ReadKey();
		}
	}
}
