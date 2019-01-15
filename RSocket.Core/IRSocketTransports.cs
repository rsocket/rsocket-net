using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketTransport
	{
		PipeReader Input { get; }
		PipeWriter Output { get; }

		Task ConnectAsync(CancellationToken cancel = default);
	}

	public interface IRSocketServerTransport
	{
		PipeReader Input { get; }
		PipeWriter Output { get; }

		Task StartAsync();
		Task StopAsync();
	}
}
