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
			//should run RSocketDemo.Server first.
			Console.WriteLine($"client started...{Thread.CurrentThread.ManagedThreadId}");
			Console.ReadKey();

			while (true)
			{
				SocketTransport socketTransport = new SocketTransport("tcp://127.0.0.1:8888/");
				_client = new RSocketDemoClient(socketTransport, new RSocketOptions() { InitialRequestSize = int.MaxValue });
				await _client.ConnectAsync();

				await RequestFireAndForgetTest();

				await RequestResponseTest();

				await RequestStreamTest();
				await RequestStreamTest1();

				await RequestChannelTest();
				await RequestChannelTest1();
				await RequestChannelTest2(); //backpressure

				await ErrorTest();

				Console.WriteLine("-----------------------------------over-----------------------------------");
				Console.ReadKey();
			}

			Console.ReadKey();
		}

		static async Task RequestFireAndForgetTest()
		{
			await _client.RequestFireAndForget("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence());

			Console.WriteLine($"RequestFireAndForget over");
			Console.ReadKey();
		}

		static async Task RequestResponseTest()
		{
			var result = await _client.RequestResponse("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence());

			Console.WriteLine($"server message: {result.Data.ConvertToString()}");

			Console.WriteLine($"RequestResponse over");
			Console.ReadKey();
		}

		static async Task RequestStreamTest()
		{
			int initialRequest = int.MaxValue;
			var result = _client.RequestStream("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), initialRequest);

			await foreach (var item in result.ToAsyncEnumerable())
			{
				Console.WriteLine($"server message: {item.Data.ConvertToString()}");
			}

			Console.WriteLine($"RequestStream over");
			Console.ReadKey();
		}
		static async Task RequestStreamTest1()
		{
			int initialRequest = 2;
			//int initialRequest = int.MaxValue;

			var result = _client.RequestStream("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), initialRequest);

			StreamSubscriber subscriber = new StreamSubscriber(initialRequest);
			subscriber.MaxReceives = 5;
			var subscription = result.Subscribe(subscriber);
			subscriber.OnSubscribe(subscription);

			await subscriber.Block();

			Console.WriteLine($"server message total: {subscriber.MsgList.Count}");

			Console.WriteLine($"RequestStream over");
			Console.ReadKey();
		}


		static async Task RequestChannelTest()
		{
			//int initialRequest = 2;
			int initialRequest = int.MaxValue;

			var result = RequestChannel(2, initialRequest);
			await foreach (var item in result.ToAsyncEnumerable())
			{
				Console.WriteLine($"server message: {item.Data.ConvertToString()}");
			}

			Console.WriteLine($"RequestChannel over");
			Console.ReadKey();
		}
		static async Task RequestChannelTest1()
		{
			int initialRequest = 2;
			//int initialRequest = int.MaxValue;

			var result = RequestChannel(10, initialRequest);

			StreamSubscriber subscriber = new StreamSubscriber(initialRequest);
			subscriber.MaxReceives = 5;
			var subscription = result.Subscribe(subscriber);
			subscriber.OnSubscribe(subscription);

			await subscriber.Block();

			Console.WriteLine($"server message: {subscriber.MsgList.Count}");

			Console.WriteLine($"RequestChannel over");
			Console.ReadKey();
		}
		/// <summary>
		/// Backpressure.
		/// </summary>
		/// <returns></returns>
		static async Task RequestChannelTest2()
		{
			int initialRequest = 2;
			//int initialRequest = int.MaxValue;

			var source = new OutputPublisher(_client, 10); //Create an object that supports backpressure.
			var result = _client.RequestChannel("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), source, initialRequest);

			StreamSubscriber subscriber = new StreamSubscriber(initialRequest);
			subscriber.MaxReceives = 8;
			var subscription = result.Subscribe(subscriber);
			subscriber.OnSubscribe(subscription);

			await subscriber.Block();

			Console.WriteLine($"server message: {subscriber.MsgList.Count}");

			Console.WriteLine($"RequestChannel over");
			Console.ReadKey();
		}

		static async Task ErrorTest()
		{
			//int initialRequest = 2;
			int initialRequest = int.MaxValue;

			var result = RequestChannel(20, initialRequest, metadata: 4.ToString());
			try
			{
				await foreach (var item in result.ToAsyncEnumerable())
				{
					Console.WriteLine($"server message: {item.Data.ConvertToString()}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred: {ex.Message}");
			}

			Console.WriteLine($"ErrorTest over");
			Console.ReadKey();
		}

		static IPublisher<Payload> RequestChannel(int outputs, int initialRequest, string data = "data", string metadata = "metadata")
		{
			IObserver<int> ob = null;
			var source = Observable.Create<int>(o =>
			{
				ob = o;
				Task.Run(() =>
				{
					for (int i = 0; i < outputs; i++)
					{
						Thread.Sleep(500);
						o.OnNext(i);
					}

					o.OnCompleted();
				});

				return () =>
				{
					Console.WriteLine("requester resources disposed");
				};
			}).Select(a =>
			{
				Console.WriteLine($"generate requester message: {a}");
				return new Payload($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);

			var result = _client.RequestChannel(data.ToReadOnlySequence(), metadata.ToReadOnlySequence(), source, initialRequest);

			return result;
		}
	}
}
