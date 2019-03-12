using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public class RSocketServer : RSocket
	{
		Task Handler;
		RSocketOptions Options { get; set; }

		public RSocketServer(IRSocketTransport transport, RSocketOptions options = default) : base(transport, options) { }

		public async Task ConnectAsync()
		{
			await Transport.StartAsync();
			Handler = Connect(CancellationToken.None);
		}

		public override void Setup(in RSocketProtocol.Setup value)
		{

		}

		//public virtual void IRSocketProtocol.Setup(in RSocketProtocol.Setup value)
		//{
		//}

		//public virtual void Error(in RSocketProtocol.Error message)
		//{
		//}

		//public virtual void Payload(in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		//{
		//}

		//public virtual void RequestStream(in RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		//{
		//}

		//public virtual void RequestResponse(in RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		//{
		//	Transport.Output.FlushAsync();
		//}

		//public virtual void RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		//{
		//}

		//public virtual void RequestChannel(in RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		//{
		//}


		//public class RSocketService2 : RSocketServer
		//{
		//	public Func<ReadOnlySequence<byte>, ReadOnlySequence<byte>, Task<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>> Responder { get; set; }

		//	public RSocketService2(IRSocketServerTransport transport) : base(transport)
		//	{

		//	}

		//	public void Register(Func<ReadOnlySequence<byte>, ReadOnlySequence<byte>, Task<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>> responder)
		//	{

		//	}


		//	public override void RequestResponse(in RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		//	{

		//	}
		//}
	}
}
