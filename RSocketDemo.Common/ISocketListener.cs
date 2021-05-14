using RSocket.Transports;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public interface ISocketListener : IAsyncDisposable
	{
		EndPoint EndPoint { get; }

		ValueTask<Socket> AcceptAsync(CancellationToken cancellationToken = default);

		ValueTask UnbindAsync(CancellationToken cancellationToken = default);
	}
}
