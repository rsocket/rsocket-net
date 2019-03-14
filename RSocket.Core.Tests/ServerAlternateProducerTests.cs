using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RSocket.Transports;
using async_enumerable_dotnet;

namespace RSocket.Tests
{
	[TestClass]
	public class ServerAlternateTests
	{
		LoopbackTransport Loopback;
		RSocketClient Client;
		RSocketClient.ForStrings StringClient;
		RSocketServer Server;


		[TestMethod]
		public async Task ServerRequestStreamTest()
		{
			Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
				AsyncEnumerable.Interval(TimeSpan.FromMilliseconds(10))
					.Take(3)
					.Map(i => (request.Data, request.Metadata))
					.ToAsyncEnumerable();

			var (data, metadata) = ("TEST DATA", "METADATA?_____");

			var enumerator = StringClient.RequestStream(data, metadata).GetAsyncEnumerator();
			var list = new List<string>();
			try { while (await enumerator.MoveNextAsync()) { list.Add(enumerator.Current); } }     //This is basically ToList()
			finally { enumerator.DisposeAsync().AsTask().Wait(); }

			Assert.AreEqual(3, list.Count, "Stream contents missing.");
			list.ForEach(item => Assert.AreEqual(item, data, "Stream contents mismatch."));
		}



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
}