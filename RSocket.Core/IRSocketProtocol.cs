using System;
using System.Buffers;

namespace RSocket
{
	/// <summary>
	/// Defines a handler for the raw protocol. Clients and Servers both implement parts of this interface; when they do not, they should throw NotImplementedException for that method.
	/// </summary>
	public interface IRSocketProtocol
	{
		void Setup(in RSocketProtocol.Setup message);
		void Error(in RSocketProtocol.Error message);
		void Payload(in RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestStream(in RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestResponse(in RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestFireAndForget(in RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestChannel(in RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void KeepAlive(in RSocketProtocol.KeepAlive message);
	}
}
