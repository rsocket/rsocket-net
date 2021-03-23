using RSocket.Exceptions;
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	public class RSocketServer : RSocket
	{
		Task Handler;

		RSocketProtocol.Setup _setup;
		int _hasSetup = 0;

		public RSocketServer(IRSocketTransport transport, PrefetchOptions options = default) : base(transport, options) { }

		private int StreamId = 2;       //SPEC: Stream IDs on the server MUST start at 2 and increment by 2 sequentially, such as 2, 4, 6, 8, etc.
		protected internal override int NewStreamId()
		{
			return Interlocked.Add(ref StreamId, 2);
		}

		protected override Task Process(Header header, SequenceReader<byte> reader)
		{
			if (Interlocked.CompareExchange(ref this._hasSetup, 1, 0) == 0)
			{
				if (header.Type == Types.Setup)
				{
					var setup = new Setup(header, ref reader);
					this.HandleSetup(setup);   //TODO These can have metadata! , setup.ReadMetadata(ref reader), setup.ReadData(ref reader)););

					return Task.CompletedTask;
				}

				throw new InvalidSetupException("SETUP frame must be received before any others");
			}

			return base.Process(header, reader);
		}

		public async Task ConnectAsync()
		{
			await Transport.StartAsync();
			Handler = Connect(CancellationToken.None);
		}

		protected override void HandleSetup(RSocketProtocol.Setup value)
		{
			this._setup = value;
			if (value.KeepAlive > 0 && value.Lifetime > 0)
			{
				this.StartKeepAlive(TimeSpan.FromMilliseconds(value.KeepAlive), TimeSpan.FromMilliseconds(value.Lifetime));
				RSocketProtocol.KeepAlive keepAlive = new RSocketProtocol.KeepAlive(0, false);
				keepAlive.WriteFlush(this.Transport.Output);
			}
		}
	}
}
