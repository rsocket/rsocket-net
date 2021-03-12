using RSocket;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RSocketDemo
{
	public class EchoRSocketClient : RSocketClient
	{
		public EchoRSocketClient(IRSocketTransport transport, RSocketOptions options = default) : base(transport, options)
		{
			// Request/Response
			Respond(
				request => request,                                 // requestTransform
				request =>
				{
					string data = Encoding.UTF8.GetString(request.Data.ToArray());
					Console.WriteLine($"收到服务端RequestRespond信息-{data}");
					return AsyncEnumerable.Repeat(request, 1);
				}, // producer
				result => result                                    // resultTransform
			);

			// Request/Stream
			Stream(
				request => request,                                 // requestTransform
				request =>
				{
					string data = Encoding.UTF8.GetString(request.Data.ToArray());
					Console.WriteLine($"收到服务端RequestStream信息-{data}");
					return AsyncEnumerable.Repeat(request, 2);
				}, // producer
				result => result                                    // resultTransform
			);


			this.Channeler = (request, incoming, subscription) => incoming.ToAsyncEnumerable().Select(_ =>
			{
				string data = Encoding.UTF8.GetString(_.Data.ToArray());
				Console.WriteLine($"收到服务端RequestChanneler信息-{data}");

				return _;
			});
		}


	}
}
