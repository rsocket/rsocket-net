using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
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
			Server.Responder = async request => { await Task.CompletedTask; return (request.Data, request.Metadata); };
			var response = await StringClient.RequestResponse("TEST DATA", "METADATA?_____");
			Assert.AreEqual("TEST DATA", response, "Response should round trip.");
		}

		[TestMethod]
		public async Task ServerRequestStreamTest()
		{
            Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
                AsyncEnumerable.Range(0, 3)
                    .Select(i => (request.Data, request.Metadata));

			var (data, metadata) = ("TEST DATA", "METADATA?_____");
			var list = await StringClient.RequestStream(data, metadata).ToListAsync();
			Assert.AreEqual(3, list.Count, "Stream contents missing.");
			list.ForEach(item => Assert.AreEqual(item, data, "Stream contents mismatch."));
		}

		[TestMethod]
		public async Task ServerRequestStreamBinaryDetailsTest()
		{
			var count = 20;
            Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
                AsyncEnumerable.Range(0, count)
                    .Select(i => (
                        new ReadOnlySequence<byte>(request.Data.ToArray().Skip(i).Take(1).ToArray()),
                        new ReadOnlySequence<byte>(request.Metadata.ToArray().Skip(i).Take(1).ToArray())));

			var (requestData, requestMetadata) = (Enumerable.Range(1, count).Select(i => (byte)i).ToArray(), Enumerable.Range(100, count).Select(i => (byte)i).ToArray());
			var list = await Client.RequestStream(result => (Data: result.data.ToArray(), Metadata: result.metadata.ToArray()), new ReadOnlySequence<byte>(requestData), new ReadOnlySequence<byte>(requestMetadata)).ToListAsync();
			Assert.AreEqual(count, list.Count, "Stream contents missing.");

			for (int i = 0; i < list.Count; i++)
			{
				Assert.AreEqual(requestData[i], list[i].Data[0], "Data Sequence Mismatch");
				Assert.AreEqual(requestMetadata[i], list[i].Metadata[0], "Metadata Sequence Mismatch");
			}
		}


		//[TestMethod]
		//public async Task ServerRequestChannelTest()
		//{
		//	Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) => new System.Collections.Async.AsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>(async yield =>
		//	{
		//		foreach (var index in Enumerable.Range(0, 3))
		//		{ await Task.CompletedTask; await yield.ReturnAsync((request.Data, request.Metadata)); }
		//	}).ToAsyncEnumerable();

		//	var (data, metadata) = ("TEST DATA", "METADATA?_____");
		//	var list = await StringClient.RequestStream(data, metadata).ToListAsync();
		//	Assert.AreEqual(3, list.Count, "Stream contents missing.");
		//	list.ForEach(item => Assert.AreEqual(item, data, "Stream contents mismatch."));
		//}


		[TestInitialize]
		public void TestInitialize()
		{
			Loopback = new LoopbackTransport(DuplexPipe.ImmediateOptions, DuplexPipe.ImmediateOptions);
			Client = new RSocketClient(Loopback);
			Server = new RSocketServer(Loopback.Beyond);
			Client.ConnectAsync().Wait();
			Server.ConnectAsync().Wait();
			StringClient = new RSocketClient.ForStrings(Client);
		}
	}


	public class Sample
	{
		static Random random = new Random(1234);
		public int Id = random.Next(1000000);
		public string Name = nameof(Sample) + random.Next(10000).ToString();
		public DateTime Created = DateTime.Now;

		public static implicit operator string(Sample value) => string.Join('|', value.Id, value.Name, value.Created);
		public static implicit operator ReadOnlySequence<byte>(Sample value) => new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(string.Join('|', value.Id, value.Name, value.Created)));
		public static implicit operator Sample(string value) { var values = value.Split('|'); return new Sample(values[0], values[1], values[2]); }
		public static implicit operator Sample(ReadOnlySequence<byte> value) => Encoding.UTF8.GetString(value.ToArray());

		public Sample() { }
		public Sample(string id, string name, string created) { Id = int.Parse(id); Name = name; Created = DateTime.Parse(created); }
		public ReadOnlySequence<byte> Bytes => this;
	}
}