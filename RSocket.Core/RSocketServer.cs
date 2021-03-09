using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public class RSocketServer : RSocket
	{
		Task Handler;

		public RSocketServer(IRSocketTransport transport, PrefetchOptions options = default) : base(transport, options) { }

		private int StreamId = 2;       //SPEC: Stream IDs on the server MUST start at 2 and increment by 2 sequentially, such as 2, 4, 6, 8, etc.
		protected override int NewStreamId()
		{
			return Interlocked.Add(ref StreamId, 2);
		}

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
