using RSocket.Transports;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public interface IConnectionListener : IAsyncDisposable
	{
		/// <summary>
		/// The endpoint that was bound. This may differ from the requested endpoint, such as when the caller requested that any free port be selected.
		/// </summary>
		EndPoint EndPoint { get; }

		/// <summary>
		/// Begins an asynchronous operation to accept an incoming connection.
		/// </summary>
		/// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
		/// <returns>A <see cref="ValueTask{ConnectionContext}"/> that completes when a connection is accepted, yielding the <see cref="ConnectionContext" /> representing the connection.</returns>
		ValueTask<SocketConnection> AcceptAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Stops listening for incoming connections.
		/// </summary>
		/// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
		/// <returns>A <see cref="ValueTask"/> that represents the un-bind operation.</returns>
		ValueTask UnbindAsync(CancellationToken cancellationToken = default);
	}
}
