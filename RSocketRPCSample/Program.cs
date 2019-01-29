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
		static async Task Main(string[] args)
		{
			//Create a new Client.
			var client = new RSocketClient(
				new WebSocketTransport("ws://localhost:9092/"));
			//	new SocketTransport("tcp://localhost:9091/"));

			//Bind a Service to this Client.
			var service = new EchoService(client);

			//Connect to a Server and establish communications.
			await client.ConnectAsync();

			//Make a service method call with no return to the server.
			await service.fireAndForget(new BytesValue() { Value = ByteString.CopyFromUtf8($"{nameof(EchoService.fireAndForget)}: Calling service...") });

			//Make a service method call returning a single value.
			var result = await service.requestResponse(new BytesValue() { Value = ByteString.CopyFromUtf8($"{nameof(EchoService.requestResponse)}: Calling service...") });
			Console.WriteLine($"Sample Result: {result.Value.ToStringUtf8()}");



			//Wait for a keypress to end session.
			Console.WriteLine($"Press any key to continue...");
			Console.ReadKey();
		}
	}
}