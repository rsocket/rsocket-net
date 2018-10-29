using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketTransport
	{
		PipeReader Input { get; }
		PipeWriter Output { get; }
		bool UseLength { get; }

		Task ConnectAsync();
	}
}
