using System;
using System.Threading.Tasks;
using System.Reactive.Linq;
using RSocket;
using RSocket.Transport;
using System.Text;

namespace RSocketSample
{
	using RSocket.Reactive;

	class Program
	{
		static async Task Main(string[] args)
		{
			Console.WriteLine("Connecting...");

			//var client = new RSocketClient(new RSocketWebSocketClient("ws://rsocket-demo.herokuapp.com/ws"));
			//await client.ConnectAsync();
			//Console.WriteLine("Requesting Web Demo Stream...");
			//await client.RequestStream("peace", initial: 2);


			//			var client = new RSocketClient(new RSocketWebSocketClient("ws://localhost:9092/"));
			var client = new RSocketClientReactive<JsonSerializer>(new RSocketWebSocketClient("ws://localhost:9092/"));
			await client.ConnectAsync();
			Console.WriteLine("Requesting Demo Stream...");

			var obj = new Test() { Id = "1234", Value = "peace" };
			var req = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(obj));

//			var stream = from data in (await client.RequestChannel<Test>(req, initial: 3))
			var stream = from data in (await client.RequestStream<Test>(req, initial: 3))
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


	//TODO Namespace and assembly structuring

	public class JsonSerializer : IRSocketSerializer
	{
		public T Deserialize<T>(in ReadOnlySpan<byte> data) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data));
		public ReadOnlySpan<byte> Serialize<T>(in T item) => Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(item));
	}
}
