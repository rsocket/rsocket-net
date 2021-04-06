using RSocket.Transports;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	internal sealed class SocketListener : ISocketListener
	{
		private Socket _listenSocket;

		public EndPoint EndPoint { get; private set; }

		internal SocketListener(
			EndPoint endpoint)
		{
			EndPoint = endpoint;
		}

		internal void Bind()
		{
			if (_listenSocket != null)
			{
				throw new InvalidOperationException();
			}

			Socket listenSocket;

			listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			BindSocket();

			void BindSocket()
			{
				try
				{
					listenSocket.Bind(EndPoint);
				}
				catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
				{
					throw new Exception(e.Message, e);
				}
			}

			Debug.Assert(listenSocket.LocalEndPoint != null);
			EndPoint = listenSocket.LocalEndPoint;

			listenSocket.Listen(100);
			this._listenSocket = listenSocket;
		}

		public async ValueTask<Socket> AcceptAsync(CancellationToken cancellationToken = default)
		{
			while (true)
			{
				try
				{
					Debug.Assert(this._listenSocket != null, "Bind must be called first.");

					var acceptSocket = await this._listenSocket.AcceptAsync();

					//// Only apply no delay to Tcp based endpoints
					//if (acceptSocket.LocalEndPoint is IPEndPoint)
					//{
					//	acceptSocket.NoDelay = _options.NoDelay;
					//}

					return acceptSocket;
				}
				catch (ObjectDisposedException)
				{
					// A call was made to UnbindAsync/DisposeAsync just return null which signals we're done
					return null;
				}
				catch (SocketException e) when (e.SocketErrorCode == SocketError.OperationAborted)
				{
					// A call was made to UnbindAsync/DisposeAsync just return null which signals we're done
					return null;
				}
				catch (SocketException)
				{
					// The connection got reset while it was in the backlog, so we try again.
					//_trace.ConnectionReset(connectionId: "(null)");
				}
			}
		}

		public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
		{
			this._listenSocket?.Dispose();

			//_socketHandle?.Dispose();
			return default;
		}

		public ValueTask DisposeAsync()
		{
			this._listenSocket?.Dispose();

			//_socketHandle?.Dispose();

			// Dispose the memory pool
			return default;
		}
	}
}
