using System;
using System.Buffers;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using RSocket;
using RSocket.RPC;
using RSocket.Transports;

namespace RSocketRPCSample
{
	using Io.Rsocket.Rpc.Testing;
	using Google.Protobuf;
	using Google.Protobuf.WellKnownTypes;

	class Program
	{
		//TODO Connection Cleanup on Unsubscribe/failure/etc
		//TODO General Error handling -> OnError

		private class ConsoleStream : IRSocketRPCStream
		{
			public void OnCompleted()
			{
				Console.WriteLine($"Request.OnCompleted");
			}

			public void OnError(Exception error)
			{
				Console.WriteLine($"Request.OnError: {error.ToString()}");
			}

			public void OnNext((string Service, string Method, ReadOnlySequence<byte> Metadata, ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Tracing) value)
			{
				var data = Encoding.UTF8.GetString(value.Data.ToArray());
				Console.WriteLine($"Request.OnNext {value.Service}.{value.Method}({data}) [{value.Metadata.Length}, {value.Tracing.Length}]");
			}
		}

		static async Task Main(string[] args)
		{
			var methods = EchoService.GetMethods();
			methods.ForEach(method => Console.WriteLine($"Service Method: {method.Name}"));

			//var client = new RSocketClient(new RSocketWebSocketClient("ws://rsocket-demo.herokuapp.com/ws"));		//await client.RequestStream("peace", initial: 2);
			//var client = new RSocketClient(new RSocketWebSocketClient("ws://localhost:9092/"));

			var client = new RSocketClient(
				new WebSocketTransport("ws://localhost:9092/"));
				//new SocketTransport("tcp://localhost:9091/"));

			await client.ConnectAsync();


			var data = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("TEST VALUES!"));

			var service = new EchoService(client);

			var result = await service.requestResponse(new BytesValue() { Value = ByteString.CopyFromUtf8("TEST VALUES!!") });

			Console.WriteLine($"Result: {result.ToString()}");

			//var rpcclient = new RSocketRPCClient(client);
			
			//await rpcclient.RequestStream(new ConsoleStream(), "EchoService", "requestStream", data);

			Console.ReadKey();

			//Console.WriteLine("Requesting Demo Stream...");

			//var obj = new Person() { Id = 1234, Name = "Someone Person", Address = new Address() { Line1 = "123 Any Street", Line2 = "Somewhere, LOC" } };
			//var meta = new Person() { Id = 567, Name = "Meta Person", Address = new Address() { Line1 = "", Line2 = "" } };
			//var req = new ProtobufNetSerializer().Serialize(obj).ToArray(); // Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
			//																//TODO req is awkward here, probably need to have incoming and return types...

			////var rr = client.RequestStream<Person, Person, Person, Person>(data: obj);

			//var personclient = client.Of<Person, Person>();
			//var stream = from data in personclient.RequestStream(obj, meta, initial: 3)
			//				 //where value.StartsWith("q")
			//			 select data.Data;

			//using (stream.Subscribe(
			//		onNext: value => Console.WriteLine($"Demo.OnNext===>{value}"), onCompleted: () => Console.WriteLine($"Demo.OnComplete!\n")))

			//using (personclient.RequestChannel(obj).Subscribe(
			//	onNext: value => Console.WriteLine($"RequestChannel.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestChannel.OnComplete!\n")))

			//using (personclient.RequestStream(obj).Subscribe(
			//	onNext: value => Console.WriteLine($"RequestStream.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestStream.OnComplete!\n")))

			//using (personclient.RequestResponse(obj).Subscribe(
			//	onNext: value => Console.WriteLine($"RequestResponse.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestResponse.OnComplete!\n")))

			//using (personclient.RequestFireAndForget(obj).Subscribe(
			//	onNext: value => Console.WriteLine($"RequestFireAndForget.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestFireAndForget.OnComplete!\n")))
			//{
			//	Console.ReadKey();
			//}


			//var sender = from index in Observable.Interval(TimeSpan.FromSeconds(1)) select new Person() { Id = (int)index, Name = $"Person #{index:0000}" };
			//using (personclient.RequestChannel(obj).Subscribe(
			//	onNext: value => Console.WriteLine($"RequestChannel.OnNext ===>{value}"), onCompleted: () => Console.WriteLine($"RequestChannel.OnComplete!")))
			//{
			//	Console.ReadKey();
			//}
		}
	}
}