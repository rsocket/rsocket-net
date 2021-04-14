using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public interface ISocketListenerFactory
	{
		Task<ISocketListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default);
	}

	public sealed class SocketTransportFactory : ISocketListenerFactory
	{
		public SocketTransportFactory()
		{

		}

		public async Task<ISocketListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
		{
			await Task.CompletedTask;
			var transport = new SocketListener(endpoint);
			transport.Bind();
			return transport;
		}
	}
}
