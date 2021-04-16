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
		static RSocketClient _client;

		static void ReadKey()
		{
			Console.ReadKey();
		}

		static async Task Main(string[] args)
		{
			//should run RSocketDemo.Server first.
			Console.WriteLine($"Enter any key to launch the client...");
			Console.ReadKey();
			Console.WriteLine($"Client started...{Thread.CurrentThread.ManagedThreadId}");

			ClientSocketTransport socketTransport = new ClientSocketTransport("127.0.0.1", 8888);
			_client = new RSocketDemoClient(socketTransport, new RSocketOptions() { InitialRequestSize = int.MaxValue, KeepAlive = TimeSpan.FromSeconds(60), Lifetime = TimeSpan.FromSeconds(120) });
			await _client.ConnectAsync(data: Encoding.UTF8.GetBytes("setup.data"), metadata: Encoding.UTF8.GetBytes("setup.metadata"));

			while (true)
			{
				await RequestFireAndForgetTest();
				ReadKey();

				await RequestResponseTest();
				ReadKey();

				await RequestStreamTest();
				ReadKey();
				await RequestStreamTest1();
				ReadKey();

				await RequestChannelTest();
				ReadKey();
				await RequestChannelTest(metadata: "echo");
				ReadKey();
				await RequestChannelTest1();
				ReadKey();

				await RequestChannelTest_Backpressure(); //backpressure
				ReadKey();

				await ErrorTest();
				ReadKey();

				await RunParallel();
				ReadKey();

				Console.WriteLine("-----------------------------------over-----------------------------------");
				Console.ReadKey();
			}

			Console.ReadKey();
		}

		public static async Task RunParallel(int threads = 20)
		{
			List<Task> list = new List<Task>();
			for (int i = 0; i < threads; i++)
			{
				var t = Task.Run(async () =>
				{
					await RequestChannelTest();
				});

				list.Add(t);
			}

			var task = Task.WhenAll(list);

			await task;

			Console.WriteLine("RunParallel over......................................");
		}

		static async Task RequestFireAndForgetTest(string data = "data", string metadata = "metadata")
		{
			string testName = $"RequestFireAndForgetTest[{data},{metadata}]";
			Console.WriteLine($"{testName} start.............................................");
			await _client.RequestFireAndForget(data.ToReadOnlySequence(), metadata.ToReadOnlySequence());

			Console.WriteLine($"RequestFireAndForgetTest over....................................................");
		}

		static async Task RequestResponseTest(string data = "data", string metadata = "metadata")
		{
			string testName = $"RequestResponseTest[{data},{metadata}]";
			Console.WriteLine($"{testName} start.............................................");
			var result = await _client.RequestResponse(data.ToReadOnlySequence(), metadata.ToReadOnlySequence());

			Console.WriteLine($"server message: {result.Data.ConvertToString()}  {Thread.CurrentThread.ManagedThreadId}");

			Console.WriteLine($"RequestResponseTest over");
		}

		static async Task RequestStreamTest(string data = "data", string metadata = "metadata")
		{
			string testName = $"RequestStreamTest[{data},{metadata}]";
			Console.WriteLine($"{testName} start.............................................");
			int initialRequest = int.MaxValue;
			IPublisher<Payload> result = _client.RequestStream(data.ToReadOnlySequence(), metadata.ToReadOnlySequence(), initialRequest);
			result = result.ObserveOn(TaskPoolScheduler.Default);

			await result.ToAsyncEnumerable().ForEachAsync(async (a) =>
			{
				Console.WriteLine($"server message: {a.Data.ConvertToString()}");
			});

			Console.WriteLine($"{testName} over....................................................");
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
		}

		static async Task RequestChannelTest(string data = "data", string metadata = "metadata")
		{
			string testName = $"RequestChannelTest[{data},{metadata}]";
			Console.WriteLine($"{testName} start.............................................");

			//int initialRequest = 2;
			int initialRequest = int.MaxValue;

			IPublisher<Payload> result = RequestChannel(5, initialRequest, data: data, metadata: metadata);
			result = result.ObserveOn(TaskPoolScheduler.Default);
			await result.ToAsyncEnumerable().ForEachAsync(async (a) =>
			{
				Console.WriteLine($"server message: {a.Data.ConvertToString()}");
			});

			Console.WriteLine($"{testName} over....................................................");
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

			ReadKey();

			result = _client.RequestStream(data: default, metadata: "handle.request.error".ToReadOnlySequence());
			result = result.ObserveOn(TaskPoolScheduler.Default);
			try
			{
				await result.ToAsyncEnumerable().ForEachAsync(async (a) =>
				{
					Console.WriteLine($"server message: {a.Data.ConvertToString()}");
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestStream[handle.request.error]: {ex.Message}");
			}

			ReadKey();

			result = _client.RequestStream(data: default, metadata: "gen.data.error".ToReadOnlySequence());
			result = result.ObserveOn(TaskPoolScheduler.Default);
			try
			{
				await result.ToAsyncEnumerable().ForEachAsync(async (a) =>
				{
					Console.WriteLine($"server message: {a.Data.ConvertToString()}");
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestStream[gen.data.error]: {ex.Message}");
			}

			ReadKey();

			//int initialRequest = 2;
			int initialRequest = int.MaxValue;

			result = RequestChannel(10, initialRequest, metadata: "handle.request.error".ToString());
			result = result.ObserveOn(TaskPoolScheduler.Default);
			try
			{
				await result.ToAsyncEnumerable().ForEachAsync(async (a) =>
				{
					Console.WriteLine($"server message: {a.Data.ConvertToString()}");
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestChannel[handle.request.error]: {ex.Message}");
			}

			result = RequestChannel(10, initialRequest, metadata: "gen.data.error".ToString());
			result = result.ObserveOn(TaskPoolScheduler.Default);
			try
			{
				await result.ToAsyncEnumerable().ForEachAsync(async (a) =>
				{
					Console.WriteLine($"server message: {a.Data.ConvertToString()}");
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An error has occurred while executing RequestChannel[gen.data.error]: {ex.Message}");
			}

			Console.WriteLine($"ErrorTest over....................................................");
		}

		static IPublisher<Payload> RequestChannel(int outputs, int initialRequest, string data = "data", string metadata = "metadata")
		{
			IObserver<int> ob = null;
			var source = Observable.Create<int>(o =>
			{
				ob = o;

				for (int i = 0; i < outputs; i++)
				{
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
