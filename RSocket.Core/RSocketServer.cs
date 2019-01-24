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
		protected IRSocketServerTransport Transport;
		Task Handler;

		public RSocketServer(IRSocketServerTransport transport)
		{
			Transport = transport;
		}

		public void Start(CancellationToken cancel = default)
		{
			Handler = RSocketProtocol.Handler(this, Transport.Input, cancel, name: nameof(RSocketServer));
		}


		public virtual void Setup(in RSocketProtocol.Setup value)
		{
		}

		public virtual void Error(in RSocketProtocol.Error message)
		{
		}

		public virtual void Payload(in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
		}

		public virtual void RequestStream(in RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
		}

		public virtual void RequestResponse(in RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
		}

		public virtual void RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
		}

		public virtual void RequestChannel(in RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
		}
	}
}
