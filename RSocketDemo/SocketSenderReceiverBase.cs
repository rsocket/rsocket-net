using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;

namespace RSocketDemo
{
	internal abstract class SocketSenderReceiverBase : IDisposable
	{
		protected readonly Socket _socket;
		protected readonly SocketAwaitableEventArgs _awaitableEventArgs;

		protected SocketSenderReceiverBase(Socket socket, PipeScheduler scheduler)
		{
			_socket = socket;
			_awaitableEventArgs = new SocketAwaitableEventArgs(scheduler);
		}

		public void Dispose() => _awaitableEventArgs.Dispose();
	}
}
