using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using RSocket.Transports;

using IRSocketStream = System.IObserver<RSocket.PayloadContent>;

namespace RSocket.Tests
{
	public class TestServer : RSocket
	{
		public IRSocketStream Stream = new StreamReceiver();
		public List<Message> All = new List<Message>();
		public IEnumerable<Message> Server => from message in All where message.IsServer select message;
		public IEnumerable<Message> Client => from message in All where !message.IsServer select message;
		public IEnumerable<Message.Setup> Setups => from message in Server.OfType<Message.Setup>() select message;
		public IEnumerable<Message.RequestStream> RequestStreams => from message in Server.OfType<Message.RequestStream>() select message;

		public TestServer(IRSocketTransport transport) : base(transport) { }

		//public override void Setup(in RSocketProtocol.Setup value) => All.Add(new Message.Setup(value));
		//public override void RequestStream(in RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) => All.Add(new Message.RequestStream(message));


		public class Message
		{
			public bool IsServer { get; }

			public Message(bool isServer = false) { IsServer = isServer; }

			public class Setup : Message
			{
				public Int32 KeepAlive { get; }
				public Int32 Lifetime { get; }
				public byte[] ResumeToken { get; }
				public string MetadataMimeType { get; }
				public string DataMimeType { get; }

				public Setup(in RSocketProtocol.Setup from) : base(true)
				{
					KeepAlive = from.KeepAlive;
					Lifetime = from.Lifetime;
					ResumeToken = from.ResumeToken;
					MetadataMimeType = from.MetadataMimeType;
					DataMimeType = from.DataMimeType;
				}
			}

			public class RequestStream : Message
			{
				public Int32 InitialRequest { get; }

				public RequestStream(in RSocketProtocol.RequestStream from) : base(true)
				{
					InitialRequest = from.InitialRequest;
				}
			}

		}

		public class StreamReceiver : List<(byte[] Metadata, byte[] Data)>, IRSocketStream
		{
			void IObserver<PayloadContent>.OnCompleted() => this.Add((null, null));
			void IObserver<PayloadContent>.OnError(Exception error) => throw new NotImplementedException(); //Next(metadata, data);
			void IObserver<PayloadContent>.OnNext(PayloadContent value) => this.Add((value.Metadata.ToArray(), value.Data.ToArray()));
		}
	}
}
