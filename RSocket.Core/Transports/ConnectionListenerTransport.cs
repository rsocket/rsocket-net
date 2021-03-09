using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RSocket;

namespace RSocket.Transports
{
	public class ConnectionListenerTransport : IRSocketTransport
	{
		public PipeReader Input { get; }

		public PipeWriter Output { get; }

		public ConnectionListenerTransport(RSocketConnection connection)
		{
			Input = connection.Input;
			Output = connection.Output;
		}

		public Task StartAsync(CancellationToken cancel = default)
		{
			return Task.CompletedTask;
		}

		public Task StopAsync()
		{
			return Task.CompletedTask;
		}
	}
}
