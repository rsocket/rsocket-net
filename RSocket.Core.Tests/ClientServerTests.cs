using System;
using System.Buffers;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RSocket.Transports;

using IRSocketStream = System.IObserver<RSocket.Payload>;

namespace RSocket.Tests
{
	[TestClass]
	public class ClientServerTests
	{

		[TestClass]
        [Ignore]
        public class ClientTests
		{
			Lazy<RSocketClient> _Client;
			RSocketClient Client => _Client.Value;
			RSocket Socket => _Client.Value;
			LoopbackTransport Loopback;
			TestServer Server;
			IRSocketStream Stream => Server.Stream;


			[TestMethod]
			public void ServerBasicTest()
			{
				Socket.RequestStream(Stream, new Sample());
				Assert.AreNotEqual(0, Server.All.Count, "Should have at least one message");
			}

			[TestMethod]
			public void ServerSetupTest()
			{
				//TODO Initialize via policy rather than defaults.
				var client = Client;
				var setup = Server.Setups.Single();
				Assert.AreEqual(TimeSpan.FromSeconds(60).TotalMilliseconds, setup.KeepAlive, "KeepAlive parity.");
				Assert.AreEqual(TimeSpan.FromSeconds(180).TotalMilliseconds, setup.Lifetime, "Lifetime parity.");
				Assert.AreEqual(0, setup.ResumeToken.Length, "ResumeToken default.");
				Assert.AreEqual("binary", setup.MetadataMimeType, "MetadataMimeType parity.");
				Assert.AreEqual("binary", setup.DataMimeType, "DataMimeType parity.");
			}

			[TestMethod]
			public void RequestStreamTest()
			{
				Socket.RequestStream(Stream, new Sample(), initial: 5);
				Assert.AreNotEqual(5, Server.RequestStreams.Single().InitialRequest, "InitialRequest partiy.");
			}



			[TestInitialize]
			public void TestInitialize()
			{
				Loopback = new Transports.LoopbackTransport(DuplexPipe.ImmediateOptions, DuplexPipe.ImmediateOptions);
				Server = new TestServer(Loopback);
				Server.Connect();
				_Client = new Lazy<RSocketClient>(() => { var rsocket = new RSocketClient(Loopback); rsocket.ConnectAsync().Wait(); return rsocket; });
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
}
