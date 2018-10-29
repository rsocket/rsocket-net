using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketReceive
	{
		void Error(RSocketProtocol.ErrorCodes errorcode, string errordata);
	}

	public class RSocketClient
	{
		IRSocketTransport Transport;
		RSocketClientOptions Options;

		public RSocketClient(IRSocketTransport transport, RSocketClientOptions options = default)
		{
			Transport = transport;
			Options = options ?? RSocketClientOptions.Default;
		}

		public async Task ConnectAsync()
		{
			await Transport.ConnectAsync();
			var server = RSocketProtocol.Server(Transport.Input, CancellationToken.None, Transport.UseLength);
			//RSocketProtocol.Setup(Transport.Output);
			new RSocketProtocol.Setup(keepalive: TimeSpan.FromSeconds(60), lifetime: TimeSpan.FromSeconds(180), metadataMimeType: "binary", dataMimeType: "binary").Write(Transport.Output);
			await Transport.Output.FlushAsync();

			//	//setup: {
			//	//   keepAlive: 60000,
			//	//   lifetime: 180000,
			//	//   dataMimeType: 'binary',
			//	//   metadataMimeType: 'binary',
			//	// },
		}

		public async ValueTask Test(string text)
		{
			await Transport.ConnectAsync();
			var server = RSocketProtocol.Server(Transport.Input, CancellationToken.None, Transport.UseLength);
			new RSocketProtocol.Test(text).Write(Transport.Output);
			await Transport.Output.FlushAsync();
		}

		public async ValueTask RequestStream(string name, int initial = 10) //TODO Appropriate initial value.
		{
			new RSocketProtocol.RequestStream(initialRequest: initial, name).Write(Transport.Output);
			await Transport.Output.FlushAsync();
		}
	}
}
