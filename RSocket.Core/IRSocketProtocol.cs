using System;
using System.Buffers;

namespace RSocket
{
	/// <summary>
	/// Defines a handler for the raw protocol. Clients and Servers both implement parts of this interface; when they do not, they should throw NotImplementedException for that method.
	/// </summary>
	public interface IRSocketProtocol
	{
		void Setup(RSocketProtocol.Setup message);
		void KeepAlive(RSocketProtocol.KeepAlive message);
		void Error(RSocketProtocol.Error message);
		void Payload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestStream(RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestResponse(RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestChannel(RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void RequestN(RSocketProtocol.RequestN message);
		void Cancel(RSocketProtocol.Cancel message);
	}
}
