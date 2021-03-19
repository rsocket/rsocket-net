using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IFrameHandler : IDisposable
	{
		void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data);
		void HandleRequestN(RSocketProtocol.RequestN message);
		void HandleCancel(RSocketProtocol.Cancel message);
		void HandleError(RSocketProtocol.Error message);
		Task ToTask();
	}
}
