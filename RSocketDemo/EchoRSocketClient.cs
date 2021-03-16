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
	}
}
