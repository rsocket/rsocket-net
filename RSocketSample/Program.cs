using System;
using System.Threading.Tasks;
using System.Reactive.Linq;
using RSocket;
using RSocket.Transport;
using RSocket.Serializers;
using System.Text;

namespace RSocketSample
{
	using ProtoBuf;
	using RSocket.Reactive;

	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Connecting...");

			//var client = new RSocketClient(new RSocketWebSocketClient("ws://rsocket-demo.herokuapp.com/ws"));
			//await client.ConnectAsync();
			//Console.WriteLine("Requesting Web Demo Stream...");
			//await client.RequestStream("peace", initial: 2);


			//			var client = new RSocketClient(new RSocketWebSocketClient("ws://localhost:9092/"));
			var client = new RSocketClientReactive<ProtobufNetSerializer>(new RSocketWebSocketClient("ws://localhost:9092/"));
			client.ConnectAsync().Wait();
			Console.WriteLine("Requesting Demo Stream...");

			var obj = new Person() { Id = 1234, Name = "Someone Person", Address = new Address() { Line1 = "123 Any Street", Line2 = "Somewhere, LOC" } };
			var req = new ProtobufNetSerializer().Serialize(obj).ToArray();	// Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(obj));

			//TODO req is awkward here, probably need to have incoming and return types...

//			var stream = from data in (await client.RequestChannel<Test>(req, initial: 3))
			var stream = from data in (client.RequestStream<Person>(req, initial: 3))
							 //let value = Encoding.UTF8.GetString(data)
						 let value = data
						 //where value.StartsWith("q")
						 select value;


			using (stream.Subscribe(
					onNext: value => Console.WriteLine($"Demo.OnNext===>{value}"),
					onCompleted: () => Console.WriteLine($"Demo.OnComplete!")))
			{
				Console.ReadKey();
			}

			//TODO Need to cleanup connetion when no subscribers and hot/cold. Oh, and OnError

			//await client.RequestStream("peace2", initial: 2);
			//await client.RequestStream("peace3", initial: 1);


			//var client = new RSocketClient(new RSocketWebSocketClient("ws://echo.websocket.org/?encoding=text"));
			//await client.Test("Howdy Planet!!");

		}

		class Test
		{
			public string Id { get; set; }
			public string Value { get; set; }
			public override string ToString() => $"{Id}:{Value}";
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
		public T Deserialize<T>(in ReadOnlySpan<byte> data) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data));
		public ReadOnlySpan<byte> Serialize<T>(in T item) => Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(item));
	}
}
