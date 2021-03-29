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
using System.Reactive.Concurrency;

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
				SocketTransport socketTransport = new SocketTransport("127.0.0.1", 8888);
				_client = new RSocketDemoClient(socketTransport, new RSocketOptions() { InitialRequestSize = int.MaxValue, KeepAlive = TimeSpan.FromSeconds(60), Lifetime = TimeSpan.FromSeconds(120) });
				await _client.ConnectAsync(data: Encoding.UTF8.GetBytes("setup.data"), metadata: Encoding.UTF8.GetBytes("setup.metadata"));

				await RequestFireAndForgetTest();

				await RequestResponseTest();

				await RequestStreamTest();
				await RequestStreamTest1();

				await RequestChannelTest();
				await RequestChannelTest(metadata: "echo");
				await RequestChannelTest1();

				await RequestChannelTest_Backpressure(); //backpressure

				await ErrorTest();

				Console.WriteLine("-----------------------------------over-----------------------------------");
				Console.ReadKey();
			}

			Console.ReadKey();
		}

		static async Task RequestFireAndForgetTest(string data = "data", string metadata = "metadata")
		{
			string testName = $"RequestFireAndForgetTest[{data},{metadata}]";
			Console.WriteLine($"{testName} start.............................................");
			await _client.RequestFireAndForget(data.ToReadOnlySequence(), metadata.ToReadOnlySequence());

			Console.WriteLine($"RequestFireAndForgetTest over....................................................");
			Console.ReadKey();
		}

		static async Task RequestResponseTest(string data = "data", string metadata = "metadata")
		{
			string testName = $"RequestResponseTest[{data},{metadata}]";
			Console.WriteLine($"{testName} start.............................................");
			var result = await _client.RequestResponse(data.ToReadOnlySequence(), metadata.ToReadOnlySequence());

			Console.WriteLine($"server message: {result.Data.ConvertToString()}  {Thread.CurrentThread.ManagedThreadId}");

			Console.WriteLine($"RequestResponseTest over");
			Console.ReadKey();
		}

		static async Task RequestStreamTest(string data = "data", string metadata = "metadata")
		{
			string testName = $"RequestStreamTest[{data},{metadata}]";
			Console.WriteLine($"{testName} start.............................................");
			int initialRequest = int.MaxValue;
			IPublisher<Payload> result = _client.RequestStream(data.ToReadOnlySequence(), metadata.ToReadOnlySequence(), initialRequest);
			result = result.ObserveOn(TaskPoolScheduler.Default);
			await foreach (var item in result.ToAsyncEnumerable())
			{
				Console.WriteLine($"server message: {item.Data.ConvertToString()}");
			}

			Console.WriteLine($"{testName} over....................................................");
			Console.ReadKey();
		}
		static async Task RequestStreamTest1()
		{
			int initialRequest = 2;
			//int initialRequest = int.MaxValue;

			IPublisher<Payload> result = _client.RequestStream("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), initialRequest);
			result = result.ObserveOn(TaskPoolScheduler.Default);
			StreamSubscriber subscriber = new StreamSubscriber(initialRequest);
			subscriber.MaxReceives = 3;
			var subscription = result.Subscribe(subscriber);
			subscriber.OnSubscribe(subscription);

			await subscriber.Block();

			Console.WriteLine($"server message total: {subscriber.MsgList.Count}");

			Console.WriteLine($"RequestStreamTest1 over....................................................");
			Console.ReadKey();
		}

		static async Task RequestChannelTest(string data = "data", string metadata = "metadata")
		{
			string testName = $"RequestChannelTest[{data},{metadata}]";
			Console.WriteLine($"{testName} start.............................................");

			//int initialRequest = 2;
			int initialRequest = int.MaxValue;

			IPublisher<Payload> result = RequestChannel(5, initialRequest, data: data, metadata: metadata);
			result = result.ObserveOn(TaskPoolScheduler.Default);
			await foreach (var item in result.ToAsyncEnumerable())
			{
				Console.WriteLine($"server message: {item.Data.ConvertToString()} {Thread.CurrentThread.ManagedThreadId}");
			}

			Console.WriteLine($"{testName} over....................................................");
			Console.ReadKey();
		}
		static async Task RequestChannelTest1()
		{
			string testName = "RequestChannelTest1";
			Console.WriteLine($"{testName} start....................................................");
			int initialRequest = 2;
			//int initialRequest = int.MaxValue;

			IPublisher<Payload> result = RequestChannel(5, initialRequest);
			result = result.ObserveOn(TaskPoolScheduler.Default);
			StreamSubscriber subscriber = new StreamSubscriber(initialRequest);
			subscriber.MaxReceives = 3;
			var subscription = result.Subscribe(subscriber);
			subscriber.OnSubscribe(subscription);

			await subscriber.Block();

			Console.WriteLine($"server message: {subscriber.MsgList.Count}");

			Console.WriteLine($"{testName} over....................................................");
			Console.ReadKey();
		}
		/// <summary>
		/// Backpressure.
		/// </summary>
		/// <returns></returns>
		static async Task RequestChannelTest_Backpressure()
		{
			string testName = "RequestChannelTest_Backpressure";
			Console.WriteLine($"{testName} start....................................................");

			int initialRequest = 2;
			//int initialRequest = int.MaxValue;

			var source = new OutputPublisher(_client, 5); //Create an object that supports backpressure.
			IPublisher<Payload> result = _client.RequestChannel("data".ToReadOnlySequence(), "metadata".ToReadOnlySequence(), source, initialRequest);
			result = result.ObserveOn(TaskPoolScheduler.Default);
			StreamSubscriber subscriber = new StreamSubscriber(initialRequest);
			subscriber.MaxReceives = 3;
			var subscription = result.Subscribe(subscriber);
			subscriber.OnSubscribe(subscription);

			await subscriber.Block();

			Console.WriteLine($"server message: {subscriber.MsgList.Count}");

			Console.WriteLine($"{testName} over....................................................");
			Console.ReadKey();
		}

		static async Task ErrorTest()
		{
			IPublisher<Payload> result;

			await _client.RequestFireAndForget(metadata: "handle.request.error".ToReadOnlySequence());

			try
			{
				var error = await _client.RequestResponse(metadata: "handle.request.error".ToReadOnlySequence());
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestResponse: {ex.Message}");
			}

			Console.ReadKey();

			result = _client.RequestStream(data: default, metadata: "handle.request.error".ToReadOnlySequence());
			result = result.ObserveOn(TaskPoolScheduler.Default);
			try
			{
				await foreach (var item in result.ToAsyncEnumerable())
				{
					Console.WriteLine($"server message: {item.Data.ConvertToString()}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestStream[handle.request.error]: {ex.Message}");
			}

			Console.ReadKey();

			result = _client.RequestStream(data: default, metadata: "gen.data.error".ToReadOnlySequence());
			result = result.ObserveOn(TaskPoolScheduler.Default);
			try
			{
				await foreach (var item in result.ToAsyncEnumerable())
				{
					Console.WriteLine($"server message: {item.Data.ConvertToString()}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestStream[gen.data.error]: {ex.Message}");
			}

			Console.ReadKey();

			//int initialRequest = 2;
			int initialRequest = int.MaxValue;

			result = RequestChannel(10, initialRequest, metadata: "handle.request.error".ToString());
			result = result.ObserveOn(TaskPoolScheduler.Default);
			try
			{
				await foreach (var item in result.ToAsyncEnumerable())
				{
					Console.WriteLine($"server message: {item.Data.ConvertToString()}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestChannel[handle.request.error]: {ex.Message}");
			}

			result = RequestChannel(10, initialRequest, metadata: "gen.data.error".ToString());
			result = result.ObserveOn(TaskPoolScheduler.Default);
			try
			{
				await foreach (var item in result.ToAsyncEnumerable())
				{
					Console.WriteLine($"server message: {item.Data.ConvertToString()}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestChannel[gen.data.error]: {ex.Message}");
			}

			Console.WriteLine($"ErrorTest over....................................................");
			Console.ReadKey();
		}

		static IPublisher<Payload> RequestChannel(int outputs, int initialRequest, string data = "data", string metadata = "metadata")
		{
			IObserver<int> ob = null;
			var source = Observable.Create<int>(o =>
			{
				ob = o;

				for (int i = 0; i < outputs; i++)
				{
					//Thread.Sleep(500);
					o.OnNext(i);
				}

				o.OnCompleted();

				return () =>
				{
					Console.WriteLine("requester resources disposed");
				};
			}).Select(a =>
			{
				Console.WriteLine($"generate requester message: {a}");
				return new Payload($"{data}-{a}".ToReadOnlySequence(), $"{metadata}-{a}".ToReadOnlySequence());
			}
			);

			IPublisher<Payload> result = _client.RequestChannel(data.ToReadOnlySequence(), metadata.ToReadOnlySequence(), source, initialRequest);

			return result;
		}
	}
}
