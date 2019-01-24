using System;
using System.Buffers;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using RSocket;
using RSocket.Reactive;
using RSocket.Serializers;
using RSocket.Transports;

namespace RSocketSample
{
	class Program
	{
		//TODO Connection Cleanup on Unsubscribe/failure/etc
		//TODO General Error handling -> OnError

		static async Task Main(string[] args)
		{
			//var client = new RSocketClient(new RSocketWebSocketClient("ws://rsocket-demo.herokuapp.com/ws"));		//await client.RequestStream("peace", initial: 2);
			//var client = new RSocketClient(new RSocketWebSocketClient("ws://localhost:9092/"));

			var loopback = new LoopbackTransport();

			var server = new RSocketServer(loopback);
			server.Start();


			var client = new RSocketClientReactive(
				new WebSocketTransport("ws://localhost:9092/"))
				//new SocketTransport("tcp://localhost:9091/"))
			//var client = new RSocketClientReactive(new RSocketWebSocketClient("ws://localhost:9092/"))
			//var client = new RSocketClientReactive(loopback)
				.UsingProtobufNetSerialization();

			await client.ConnectAsync();
			Console.WriteLine("Requesting Demo Stream...");

			var obj = new Person() { Id = 1234, Name = "Someone Person", Address = new Address() { Line1 = "123 Any Street", Line2 = "Somewhere, LOC" } };
			var meta = new Person() { Id = 567, Name = "Meta Person", Address = new Address() { Line1 = "", Line2 = "" } };
			var req = new ProtobufNetSerializer().Serialize(obj).ToArray(); // Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
																			//TODO req is awkward here, probably need to have incoming and return types...

			//var rr = client.RequestStream<Person, Person, Person, Person>(data: obj);

			var personclient = client.Of<Person, Person>();
			var stream = from data in personclient.RequestStream(obj, meta, initial: 3)
						 //where value.StartsWith("q")
						 select data.Data;

			using (stream.Subscribe(
					onNext: value => Console.WriteLine($"Demo.OnNext===>{value}"), onCompleted: () => Console.WriteLine($"Demo.OnComplete!\n")))

			using (personclient.RequestChannel(obj).Subscribe(
				onNext: value => Console.WriteLine($"RequestChannel.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestChannel.OnComplete!\n")))

			using (personclient.RequestStream(obj).Subscribe(
				onNext: value => Console.WriteLine($"RequestStream.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestStream.OnComplete!\n")))

			using (personclient.RequestResponse(obj).Subscribe(
				onNext: value => Console.WriteLine($"RequestResponse.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestResponse.OnComplete!\n")))

			using (personclient.RequestFireAndForget(obj).Subscribe(
				onNext: value => Console.WriteLine($"RequestFireAndForget.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestFireAndForget.OnComplete!\n")))
			{ 
				Console.ReadKey();
			}


			//var sender = from index in Observable.Interval(TimeSpan.FromSeconds(1)) select new Person() { Id = (int)index, Name = $"Person #{index:0000}" };
			//using (personclient.RequestChannel(obj).Subscribe(
			//	onNext: value => Console.WriteLine($"RequestChannel.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestChannel.OnComplete!")))
			//{
			//	Console.ReadKey();
			//}
		}
	}


	[ProtoContract]
	class Person
	{
		[ProtoMember(1)] public int Id { get; set; }
		[ProtoMember(2)] public string Name { get; set; }
		[ProtoMember(3)] public Address Address { get; set; }
		public override string ToString() => $"{Id}:{Name} ({Address})";
	}

	[ProtoContract]
	class Address
	{
		[ProtoMember(1)] public string Line1 { get; set; }
		[ProtoMember(2)] public string Line2 { get; set; }
		public override string ToString() => $"{Line1},{Line2}";
	}

	//TODO Namespace and assembly structuring

	public class JsonSerializer : IRSocketSerializer
	{
		public T Deserialize<T>(in ReadOnlySequence<byte> data) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data.ToArray()));
		public ReadOnlySequence<byte> Serialize<T>(in T item) => new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(item)));
	}
}
