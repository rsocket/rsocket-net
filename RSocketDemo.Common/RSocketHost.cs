using Microsoft.Extensions.Logging;
using RSocket;
using RSocket.Transports;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public class RSocketHost
	{
		private readonly IConnectionListenerFactory _connectionListenerFactory;
		public readonly ConcurrentDictionary<string, (RSocketServer Server, Task ExecutionTask)> _connections = new ConcurrentDictionary<string, (RSocketServer, Task)>();
		private readonly ILogger<RSocketHost> _logger;

		private IConnectionListener _connectionListener;

		IPEndPoint _ip;

		Func<IRSocketTransport, RSocketServer> _serverBuilder;

		public RSocketHost(IConnectionListenerFactory connectionListenerFactory, IPEndPoint ip, Func<IRSocketTransport, RSocketServer> serverBuilder)
		{
			_connectionListenerFactory = connectionListenerFactory;
			_ip = ip;
			this._serverBuilder = serverBuilder;
		}

		public async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_connectionListener = await _connectionListenerFactory.BindAsync(this._ip, stoppingToken);

			while (true)
			{
				SocketConnection connection = await _connectionListener.AcceptAsync(stoppingToken);
				Console.WriteLine($"client[{connection.ConnectionId}] established...");
				// AcceptAsync will return null upon disposing the listener
				if (connection == null)
				{
					break;
				}

				RSocketServer server = this._serverBuilder(connection);
				_connections[connection.ConnectionId] = (server, Accept(server, connection.ConnectionId));
			}

			List<Task> connectionsExecutionTasks = new List<Task>(_connections.Count);

			foreach (var connection in _connections)
			{
				connectionsExecutionTasks.Add(connection.Value.ExecutionTask);
				//connection.Value.Context.Abort();
			}

			await Task.WhenAll(connectionsExecutionTasks);
		}

		//public override async Task StopAsync(CancellationToken cancellationToken)
		//{
		//	await _connectionListener.DisposeAsync();
		//}

		private async Task Accept(RSocketServer server, string connectionId)
		{
			try
			{
				await Task.Yield();

				await server.ConnectAsync();

				//_logger.LogInformation("Connection {ConnectionId} connected", connectionContext.ConnectionId);

				//await connectionContext.ConnectionClosed.WaitAsync();
			}
			//catch (ConnectionResetException)
			//{ }
			catch (ConnectionAbortedException)
			{ }
			catch (Exception e)
			{
				//_logger.LogError(e, "Connection {ConnectionId} threw an exception", connectionContext.ConnectionId);
			}
			finally
			{
				//await connectionContext.DisposeAsync();

				_connections.TryRemove(connectionId, out _);

				//_logger.LogInformation("Connection {ConnectionId} disconnected", connectionContext.ConnectionId);
			}
		}
	}

}
