using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;

namespace RSocket.RPC.Tests
{
	public class RPCTestServer : RSocketServer
	{
		//public IRSocketStream Stream = new StreamReceiver();
		//public List<Message> All = new List<Message>();
		//public IEnumerable<Message> Server => from message in All where message.IsServer select message;
		//public IEnumerable<Message> Client => from message in All where !message.IsServer select message;
		//public IEnumerable<Message.Setup> Setups => from message in Server.OfType<Message.Setup>() select message;
		//public IEnumerable<Message.RequestStream> RequestStreams => from message in Server.OfType<Message.RequestStream>() select message;

		public RPCTestServer(IRSocketServerTransport transport) : base(transport) { }

		//public override void Setup(in RSocketProtocol.Setup value) => All.Add(new Message.Setup(value));
		public override void RequestResponse(in RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			new RSocketProtocol.Payload(message.Stream, data, metadata, complete: true, next: true).Write(Transport.Output, data, metadata);
			base.RequestResponse(message, metadata, data);
		}

		//public class Message
		//{
		//	public bool IsServer { get; }

		//	public Message(bool isServer = false) { IsServer = isServer; }

		//	public class Setup : Message
		//	{
		//		public Int32 KeepAlive { get; }
		//		public Int32 Lifetime { get; }
		//		public byte[] ResumeToken { get; }
		//		public string MetadataMimeType { get; }
		//		public string DataMimeType { get; }

		//		public Setup(in RSocketProtocol.Setup from) : base(true)
		//		{
		//			KeepAlive = from.KeepAlive;
		//			Lifetime = from.Lifetime;
		//			ResumeToken = from.ResumeToken;
		//			MetadataMimeType = from.MetadataMimeType;
		//			DataMimeType = from.DataMimeType;
		//		}
		//	}

		//	public class RequestStream : Message
		//	{
		//		public Int32 InitialRequest { get; }

		//		public RequestStream(in RSocketProtocol.RequestStream from) : base(true)
		//		{
		//			InitialRequest = from.InitialRequest;
		//		}
		//	}

		//}

		//public class StreamReceiver : List<(byte[] Metadata, byte[] Data)>, IRSocketStream
		//{
		//	void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnCompleted() => this.Add((null, null));
		//	void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnError(Exception error) => throw new NotImplementedException(); //Next(metadata, data);
		//	void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnNext((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value) => this.Add((value.metadata.ToArray(), value.data.ToArray()));
		//}
	}
}
