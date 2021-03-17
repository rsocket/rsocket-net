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


		}

		public override void HandleRequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Console.WriteLine($"Received RequestFireAndForget msg: {data.ConvertToString()}");
		}
	}
}
