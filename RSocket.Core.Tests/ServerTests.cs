using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RSocket.Collections.Generic;
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
		public void ServerRequestStreamTest()
		{ 
			Server.Streamer =
			//			IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> Streamer
			((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
			{
				return Yield().AsyncEnumerate();

				IEnumerable<Task<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)>> Yield()
				{
					yield return Delay(TimeSpan.FromMilliseconds(10), Task.FromResult((request.Data, request.Metadata)));
					yield return Delay(TimeSpan.FromMilliseconds(10), Task.FromResult((request.Data, request.Metadata)));
					yield return Delay(TimeSpan.FromMilliseconds(10), Task.FromResult((request.Data, request.Metadata)));
					async Task<T> Delay<T>(TimeSpan delay, Task<T> yield) { await Task.Delay(delay); return await yield; }
				}
			};

			//var astream = StringClient.RequestStream("TEST DATA", "METADATA?_____");

			var list = new List<string>();
			var data = "TEST DATA";
			var metadata = "METADATA?_____";

			var source = Client.RequestStream<string>(value => Encoding.UTF8.GetString(value.data.ToArray()),
				new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)),
				metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
			var enumerator = source.GetAsyncEnumerator();
			try { while (enumerator.MoveNextAsync().Result) { list.Add(enumerator.Current); } }
			finally { enumerator.DisposeAsync().AsTask().Wait(); }

	//		var stream = astream.ToEnumerable().ToList();
	//		Assert.AreEqual(3, stream.Count, "Stream contents missing.");
		}

		[TestInitialize]
		public void TestInitialize()
		{
			Loopback = new Transports.LoopbackTransport(DuplexPipe.ImmediateOptions, DuplexPipe.ImmediateOptions);
			Client = new RSocketClient(Loopback);
			Server = new RSocketServer(Loopback.Beyond);
			Client.ConnectAsync().Wait();
			Server.ConnectAsync().Wait();
			StringClient = new RSocketClient.ForStrings(Client);
			//_Client = new Lazy<RSocketClient>(() => new RSocketClient(Loopback).ConnectAsync().Result);
			//RSocketClient Client => _Client.Value; Lazy<RSocketClient> _Client;
			//RSocketServer Server => _Server.Value; Lazy<RSocketServer> _Server;
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