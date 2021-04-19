using RSocket;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public class RSocketDemoClient : RSocketClient
	{
		public RSocketDemoClient(IRSocketTransport transport, RSocketOptions options = default) : base(transport, options)
		{
			this.FireAndForgetHandler = this.ForRequestFireAndForget;
		}

		async Task ForRequestFireAndForget((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			await Task.CompletedTask;
			Console.WriteLine($"Received RequestFireAndForget msg: {request.Data.ConvertToString()}");
		}
	}
}
