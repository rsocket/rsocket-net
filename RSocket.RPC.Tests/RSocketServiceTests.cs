using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RSocket.RPC.Tests
{
	using System.Buffers;
	using System.Threading.Tasks;
	using RSocket.Transports;

	[TestClass]
	public class RSocketServiceTests
	{
		Lazy<RSocketClient> _Client;
		RSocketClient Client => _Client.Value;
		TestService Service;
		LoopbackTransport Loopback;
		RPCTestServer Server;
		//IRSocketStream Stream => Server.Stream;


		[TestMethod]
		public void ServerBasicTest()
		{
			var data = new TestData();
			var response = Service.RequestResponse(data).Result;
			var result = new TestData(response);

			Assert.AreEqual(data, result, $"{nameof(Service.RequestResponse)} did not round trip on bytes.");

			//public Task<ReadOnlySequence<byte>> requestResponse(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) => base.RequestResponse<ReadOnlySequence<byte>>(ServicePrefix + nameof(EchoService), nameof(requestResponse), data, metadata);
			//public Task<ReadOnlySequence<byte>> requestStream(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) => base.RequestStream<ReadOnlySequence<byte>>(ServicePrefix + nameof(EchoService), nameof(requestStream), data, metadata);

			//Client.RequestStream(Stream, new Sample().Bytes);
			//Assert.AreNotEqual(0, Server.All.Count, "Should have at least one message");
		}


		[System.Runtime.CompilerServices.CompilerGenerated]
		public class TestService : RSocketService
		{
			private const string SERVICE = nameof(TestService);
			public TestService(RSocketClient client) : base(client) { }
			//public void fireAndForget(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestFireAndForget(null, data, metadata); }
			public Task<ReadOnlySequence<byte>> RequestResponse(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) => __RequestResponse(data, metadata, service: SERVICE);
			//public Task<ReadOnlySequence<byte>> RequestStream(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) => base.__RequestStream<ReadOnlySequence<byte>>(ServicePrefix + nameof(TestService), nameof(RequestStream), data, metadata);
			//public void requestChannel(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestChannel(null, data, metadata); }
		}



		[TestMethod]
		public void SupportTests()
		{
			var data = new TestData();
			var bytecopy = new TestData(data);
			Assert.AreEqual(data.GetHashCode(), bytecopy.GetHashCode(), $"TestData byte constructor did not result in hascode equality.");
			Assert.AreEqual(data, bytecopy, $"TestData byte constructor did not result in equality.");
		}

		public class TestData
		{
			public Guid Id = Guid.NewGuid();
			public TestData() { }
			public TestData(ReadOnlySequence<byte> from) { Id = new Guid(from.ToArray()); }
			public static implicit operator ReadOnlySequence<byte>(TestData from) => new ReadOnlySequence<byte>(from.Id.ToByteArray());
			public override string ToString() => nameof(TestData) + Id.ToString();
			public override int GetHashCode() => HashCode.Combine(Id);
			public override bool Equals(object obj) => (obj is TestData other) && this.Id == other.Id;
		}

		[TestInitialize]
		public void TestInitialize()
		{
			Loopback = new Transports.LoopbackTransport(DuplexPipe.ImmediateOptions, DuplexPipe.ImmediateOptions);
			Server = new RPCTestServer(Loopback);
			Server.Start();
			_Client = new Lazy<RSocketClient>(() => new RSocketClient(Loopback).ConnectAsync().Result);
			Service = new TestService(Client);
		}
	}
}
