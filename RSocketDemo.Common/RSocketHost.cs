using Microsoft.Extensions.Logging;
using RSocket;
using RSocket.Transports;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public class RSocketHost
	{
		readonly ISocketListenerFactory _socketListenerFactory;
		public readonly ConcurrentDictionary<string, (RSocketServer Server, Task ExecutionTask)> _servers = new ConcurrentDictionary<string, (RSocketServer, Task)>();
		readonly ILogger<RSocketHost> _logger;

		ISocketListener _socketListener;

		IPEndPoint _ip;

		Func<IRSocketTransport, RSocketServer> _serverBuilder;

		public RSocketHost(ISocketListenerFactory socketListenerFactory, IPEndPoint ip, Func<IRSocketTransport, RSocketServer> serverBuilder)
		{
			this._socketListenerFactory = socketListenerFactory;
			this._ip = ip;
			this._serverBuilder = serverBuilder;
		}

		public async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			this._socketListener = await this._socketListenerFactory.BindAsync(this._ip, stoppingToken);

			while (true)
			{
				Socket socket = await this._socketListener.AcceptAsync(stoppingToken);

				// AcceptAsync will return null upon disposing the listener
				if (socket == null)
				{
					break;
				}

				string connectionId = Guid.NewGuid().ToString();

#if DEBUG
				Console.WriteLine($"client[{connectionId}] established...");
#endif

				ServerSocketTransport transport = new ServerSocketTransport(socket);
				RSocketServer server = this._serverBuilder(transport);
				this._servers[connectionId] = (server, Accept(server, transport, connectionId));
			}

			List<Task> connectionsExecutionTasks = new List<Task>(this._servers.Count);

			foreach (var connection in this._servers)
			{
				connectionsExecutionTasks.Add(connection.Value.ExecutionTask);
			}

			await Task.WhenAll(connectionsExecutionTasks);
		}

		async Task Accept(RSocketServer server, SocketTransport transport, string connectionId)
		{
			try
			{
				await Task.Yield();

				await server.ConnectAsync();

				//_logger.LogInformation("Connection {ConnectionId} connected", connectionContext.ConnectionId);

				await transport.Running;
				//await connectionContext.ConnectionClosed.WaitAsync();
			}
			//catch (ConnectionResetException)
			//{ }
			catch (ConnectionAbortedException)
			{
			}
			catch (Exception e)
			{
				//_logger.LogError(e, "Connection {ConnectionId} threw an exception", connectionContext.ConnectionId);
			}
			finally
			{
				//await connectionContext.DisposeAsync();
				server.Dispose();
				this._servers.TryRemove(connectionId, out _);

#if DEBUG
				Console.WriteLine($"Connection {connectionId} disconnected");
#endif

				//_logger.LogInformation("Connection {ConnectionId} disconnected", connectionContext.ConnectionId);
			}
		}
	}
}
