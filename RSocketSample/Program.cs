using System;
using System.Buffers;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using RSocket;
using RSocket.Transports;

namespace RSocketSample
{
	class Program
	{
		//TODO Connection Cleanup on Unsubscribe/failure/etc
		//TODO General Error handling -> OnError

		static async Task Main(string[] args)
		{
			var loopback = new LoopbackTransport();
			var server = new EchoServer(loopback.Beyond);
			await server.ConnectAsync();

			//var client = new RSocketClient(new SocketTransport("tcp://localhost:9091/"), new RSocketOptions() { InitialRequestSize = 3 });
			//var client = new RSocketClient(new WebSocketTransport("ws://localhost:9092/"), new RSocketOptions() { InitialRequestSize = 3 });
			var client = new RSocketClient(loopback, new RSocketOptions() { InitialRequestSize = int.MaxValue });
			await client.ConnectAsync();


			Console.WriteLine("Requesting Raw Protobuf Stream...");

			var persondata = new Person() { Id = 1234, Name = "Someone Person", Address = new Address() { Line1 = "123 Any Street", Line2 = "Somewhere, LOC" } };
			var personmetadata = new Person() { Id = 567, Name = "Meta Person", Address = new Address() { Line1 = "", Line2 = "" } };

			//Make a Raw binary call just to show how it's done.
			//var stream = client.RequestStream(
			//	resultmapper: result => (Data: ProtobufNetSerializer.Deserialize<Person>(result.Data), Metadata: ProtobufNetSerializer.Deserialize<Person>(result.Metadata)),
			//	data: ProtobufNetSerializer.Serialize(persondata), metadata: ProtobufNetSerializer.Serialize(personmetadata));

			var stream = client.RequestStream(ProtobufNetSerializer.Serialize(persondata), ProtobufNetSerializer.Serialize(personmetadata));

			await stream.ForEachAsync(persons => Console.WriteLine($"RawDemo.OnNext===>[{ProtobufNetSerializer.Deserialize<Person>(persons.Metadata)}]{ProtobufNetSerializer.Deserialize<Person>(persons.Data)}"));


			Console.WriteLine("\nRequesting String Serializer Stream...");

			await client.RequestStream("A Demo Payload")
				.ForEachAsync(result => Console.WriteLine($"StringDemo.OnNext===>{result}"));

			Console.ReadKey();

			//var sender = from index in Observable.Interval(TimeSpan.FromSeconds(1)) select new Person() { Id = (int)index, Name = $"Person #{index:0000}" };
			//using (personclient.RequestChannel(obj).Subscribe(
			//	onNext: value => Console.WriteLine($"RequestChannel.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestChannel.OnComplete!")))
			//{
			//	Console.ReadKey();
			//}
		}
	}

	class EchoServer : RSocketServer
	{
		int _echoes = 1;

		public EchoServer(IRSocketTransport transport, RSocketOptions options = default, int echoes = 2) : base(transport, options)
		{
			this._echoes = echoes;
			this.Streamer = this.ForRequestStream;
		}

		public IObservable<Payload> ForRequestStream((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			return AsyncEnumerable.Repeat(request, this._echoes).Select(a => new Payload(a.Data, a.Metadata)).ToObservable();
		}
	}
}
