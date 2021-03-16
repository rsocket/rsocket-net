using RSocket;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

namespace RSocketDemo
{
	internal class EchoServer : RSocketServer
	{
		public string ConnectionId { get; set; }
		public EchoServer(IRSocketTransport transport, RSocketOptions options = default, int echoes = 2)
			: base(transport, options)
		{

 
		}
	}
}
