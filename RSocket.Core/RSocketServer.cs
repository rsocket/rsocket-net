using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public class RSocketServer : IRSocketProtocol
	{
		IRSocketServerTransport Transport;
		Task Handler;

		public RSocketServer(IRSocketServerTransport transport)
		{
			Transport = transport;
		}

		public void Start(CancellationToken cancel = default)
		{
			Handler = RSocketProtocol.Handler2(this, Transport.Input, cancel);
		}


		public virtual void Setup(in RSocketProtocol.Setup value)
		{
		}

		public virtual void Payload(in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
		}

		public virtual void RequestStream(in RSocketProtocol.RequestStream message)
		{
		}

		public virtual void Error(in RSocketProtocol.Error message)
		{
		}
	}
}
