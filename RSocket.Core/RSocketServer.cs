using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public class RSocketServer : RSocket
	{
		Task Handler;

		public RSocketServer(IRSocketTransport transport, PrefetchOptions options = default) : base(transport, options) { }

		public async Task ConnectAsync()
		{
			await Transport.StartAsync();
			Handler = Connect(CancellationToken.None);
		}

		public override void Setup(in RSocketProtocol.Setup value)
		{

		}
	}
}
