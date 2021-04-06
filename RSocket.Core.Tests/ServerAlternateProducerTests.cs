using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RSocket.Transports;
using System.Linq;
using System.Reactive.Linq;

namespace RSocket.Tests
{
	[TestClass]
	public class ServerAlternateProducerTests
	{
		LoopbackTransport Loopback;
		RSocketClient Client;
		RSocketClient.ForStrings StringClient;
		RSocketServer Server;


		[TestMethod]
		public async Task ServerRequestStreamTest()
		{
			Server.Streamer = ((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request) =>
			{
				return Observable.Interval(TimeSpan.FromMilliseconds(10))
						.Take(3)
						.Select(i => new Payload(request.Data, request.Metadata));
			};

			var (data, metadata) = ("TEST DATA", "METADATA?_____");

			var list = await StringClient.RequestStream(data, metadata)
				.ToListAsync();

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
