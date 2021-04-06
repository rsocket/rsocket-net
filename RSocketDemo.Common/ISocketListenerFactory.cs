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
		ValueTask<ISocketListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default);
	}

	public sealed class SocketTransportFactory : ISocketListenerFactory
	{

		public SocketTransportFactory()
		{

		}

		public ValueTask<ISocketListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
		{
			var transport = new SocketListener(endpoint);
			transport.Bind();
			return new ValueTask<ISocketListener>(transport);
		}
	}
}
