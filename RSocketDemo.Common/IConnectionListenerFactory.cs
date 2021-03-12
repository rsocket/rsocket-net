using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public interface IConnectionListenerFactory
	{
		ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default);
	}

	public sealed class SocketTransportFactory : IConnectionListenerFactory
	{

		public SocketTransportFactory()
		{
			//if (options == null)
			//{
			//	throw new ArgumentNullException(nameof(options));
			//}

			//if (loggerFactory == null)
			//{
			//	throw new ArgumentNullException(nameof(loggerFactory));
			//}

			//_options = options.Value;
			//var logger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets");
			//_trace = new SocketsTrace(logger);
		}

		public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
		{
			var transport = new SocketConnectionListener(endpoint);
			transport.Bind();
			return new ValueTask<IConnectionListener>(transport);
		}
	}
}
