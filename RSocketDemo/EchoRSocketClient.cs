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
			//// Request/Response
			//Respond(
			//	request => request,                                 // requestTransform
			//	request =>
			//	{
			//		string data = Encoding.UTF8.GetString(request.Data.ToArray());
			//		Console.WriteLine($"收到服务端RequestRespond信息-{data}");
			//		return AsyncEnumerable.Repeat(request, 1);
			//	}, // producer
			//	result => result                                    // resultTransform
			//);
		}


	}
}
