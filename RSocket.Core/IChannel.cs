using System;
using System.Buffers;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IChannel : IDisposable
	{
		int ChannelId { get; set; }

		void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void HandleRequestN(RSocketProtocol.RequestN message);
		void HandleCancel(RSocketProtocol.Cancel message);
		void HandleError(RSocketProtocol.Error message);
		Task ToTask();
	}
}
