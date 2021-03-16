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
	internal class RSocketHost
	{
		private readonly IConnectionListenerFactory _connectionListenerFactory;
		public readonly ConcurrentDictionary<string, (EchoServer Server, Task ExecutionTask)> _connections = new ConcurrentDictionary<string, (EchoServer, Task)>();
		private readonly ILogger<RSocketHost> _logger;

		private IConnectionListener _connectionListener;

		IPEndPoint _ip;

		public RSocketHost(IConnectionListenerFactory connectionListenerFactory, IPEndPoint ip)
		{
			_connectionListenerFactory = connectionListenerFactory;
			_ip = ip;
		}

		public async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_connectionListener = await _connectionListenerFactory.BindAsync(this._ip, stoppingToken);

			while (true)
			{
				RSocketConnection connectionContext = await _connectionListener.AcceptAsync(stoppingToken);
				Console.WriteLine($"client[{connectionContext.ConnectionId}] established...");
				// AcceptAsync will return null upon disposing the listener
				if (connectionContext == null)
				{
					break;
				}

				IRSocketTransport rsocketTransport = new ConnectionListenerTransport(connectionContext);
				EchoServer rchoServer = new EchoServer(rsocketTransport);
				rchoServer.ConnectionId = connectionContext.ConnectionId;

				_connections[connectionContext.ConnectionId] = (rchoServer, Accept(rchoServer));
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

		private async Task Accept(EchoServer server)
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

				_connections.TryRemove(server.ConnectionId, out _);

				//_logger.LogInformation("Connection {ConnectionId} disconnected", connectionContext.ConnectionId);
			}
		}
	}

}
