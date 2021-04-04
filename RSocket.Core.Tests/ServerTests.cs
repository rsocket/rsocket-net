using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RSocket.Transports;

namespace RSocket.Tests
{
	[TestClass]
	public class ServerTests
	{
		LoopbackTransport Loopback;
		RSocketClient Client;
		RSocketClient.ForStrings StringClient;
		RSocketServer Server;


		[TestMethod]
		public void ServerBasicTest()
		{
			Assert.AreNotEqual(Loopback.Input, Loopback.Beyond.Input, "Loopback Client/Server Inputs shouldn't be same.");
			Assert.AreNotEqual(Loopback.Output, Loopback.Beyond.Output, "Loopback Client/Server Outputs shouldn't be same.");
		}

		[TestMethod]
		public async Task ServerRequestResponseTest()
		{
			Server.Responder = async request => { await Task.CompletedTask; return new Payload(request.Data, request.Metadata); };
			var response = await StringClient.RequestResponse("TEST DATA", "METADATA?_____");
			Assert.AreEqual("TEST DATA", response, "Response should round trip.");
		}

		[TestMethod]
		public async Task ServerRequestStreamTest()
		{
			Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
				Observable.Range(0, 3)
					.Select(i => new Payload(request.Data, request.Metadata));

			var (data, metadata) = ("TEST DATA", "METADATA?_____");
			var list = await StringClient.RequestStream(data, metadata).ToListAsync();
			Assert.AreEqual(3, list.Count, "Stream contents missing.");
			list.ForEach(item => Assert.AreEqual(item, data, "Stream contents mismatch."));
		}

		[TestMethod]
		public async Task ServerRequestStreamBinaryDetailsTest()
		{
			var count = 20;
			//Server.Stream(request => (Data: request.Data.ToArray(), Metadata: request.Metadata.ToArray()),
			//	request => from index in AsyncEnumerable.Range(0, count) select (Data: request.Data.Skip(index).Take(1), Metadata: request.Metadata.Skip(index).Take(1)),
			//	result => (
			//		new ReadOnlySequence<byte>(result.Data.ToArray()),
			//		new ReadOnlySequence<byte>(result.Metadata.ToArray()))
			//	);

			Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
				{
					var (requestData, requestMetadata) = (request.Data.ToArray(), request.Metadata.ToArray());

					var outgoing = from index in AsyncEnumerable.Range(0, count) select (Data: requestData.Skip(index).Take(1), Metadata: requestMetadata.Skip(index).Take(1));

					return outgoing.Select(a => new Payload(new ReadOnlySequence<byte>(a.Data.ToArray()), new ReadOnlySequence<byte>(a.Metadata.ToArray()))).ToObservable();
				};

			//TODO Split into separate test - this is a good pattern for some things.
			//Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
			//    AsyncEnumerable.Range(0, count)
			//        .Select(i => (
			//            new ReadOnlySequence<byte>(request.Data.ToArray().Skip(i).Take(1).ToArray()),
			//            new ReadOnlySequence<byte>(request.Metadata.ToArray().Skip(i).Take(1).ToArray())));

			var (requestData, requestMetadata) = (Enumerable.Range(1, count).Select(i => (byte)i).ToArray(), Enumerable.Range(100, count).Select(i => (byte)i).ToArray());
			var list = await Client.RequestStream(new ReadOnlySequence<byte>(requestData), new ReadOnlySequence<byte>(requestMetadata)).Select(a => (Data: a.Data.ToArray(), Metadata: a.Metadata.ToArray())).ToAsyncEnumerable().ToListAsync();

			Assert.AreEqual(count, list.Count, "Stream contents missing.");

			for (int i = 0; i < list.Count; i++)
			{
				Assert.AreEqual(requestData[i], list[i].Data[0], "Data Sequence Mismatch");
				Assert.AreEqual(requestMetadata[i], list[i].Metadata[0], "Metadata Sequence Mismatch");
			}
		}


		[TestMethod, Ignore]
		public async Task ServerRequestChannelTest()
		{
			//Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>, IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>> channeler =
			//	(request, incoming) => incoming.ToAsyncEnumerable();
			Server.Channeler = (request, incoming) =>
			{
				var echo = Observable.Create<Payload>(observer =>
				{
					var sub = incoming.Subscribe(observer);
					sub.Request(int.MaxValue);

					return Disposable.Empty;
				});

				return echo;
			};

			//Server.Channeler = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request, IObservable<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> incoming) =>
			//{
			//	Action<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> onNext = value => { };
			//	Action OnCompleted = () => { };
			//	var enumerable = new System.Collections.Async.AsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>(async yield =>
			//	{
			//		foreach (var index in Enumerable.Range(0, 3))
			//		{ await Task.CompletedTask; await yield.ReturnAsync((request.Data, request.Metadata)); }
			//	}).ToAsyncEnumerable();
			//	return (onNext, OnCompleted, enumerable);
			//};

			var (data, metadata) = ("TEST DATA", "METADATA?_____");
			var list = await StringClient.RequestChannel(AsyncEnumerable.Range(0, 2).Select(i => $"INPUT {i}"), data, metadata).ToListAsync();
			Assert.AreEqual(2, list.Count, "Stream contents missing.");
			//list.ForEach(item => Assert.AreEqual(item, data, "Stream contents mismatch."));
		}


		[TestInitialize]
		public void TestInitialize()
		{
			Loopback = new LoopbackTransport(DuplexPipe.ImmediateOptions, DuplexPipe.ImmediateOptions);
			Client = new RSocketClient(Loopback, new RSocketOptions() { InitialRequestSize = int.MaxValue });
			Server = new RSocketServer(Loopback.Beyond);
			Client.ConnectAsync().Wait();
			Server.ConnectAsync().Wait();
			StringClient = new RSocketClient.ForStrings(Client);
		}
	}
}
