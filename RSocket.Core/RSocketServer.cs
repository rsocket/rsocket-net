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

		int _hasSetup = 0;

		protected RSocketProtocol.Setup Setup { get; set; }

		public RSocketServer(IRSocketTransport transport, PrefetchOptions options = default) : base(transport, options) { }

		private int StreamId = 2;       //SPEC: Stream IDs on the server MUST start at 2 and increment by 2 sequentially, such as 2, 4, 6, 8, etc.
		protected internal override int NewStreamId()
		{
			return Interlocked.Add(ref StreamId, 2);
		}

		protected sealed override Task Process(Header header, SequenceReader<byte> reader)
		{
			if (Interlocked.CompareExchange(ref this._hasSetup, 1, 0) == 0)
			{
				if (header.Type == Types.Setup)
				{
					try
					{
						var setup = new Setup(header, ref reader);
						this.Setup = setup;
						this.HandleSetup(setup, setup.ReadMetadata(reader), setup.ReadData(reader));
					}
					catch (RSocketErrorException)
					{
						throw;
					}
					catch (Exception ex)
					{
#if DEBUG
						Console.WriteLine($"{ex.Message} {ex.StackTrace}");
#endif
						throw new RejectedSetupException($"An exception occurred while handling setup: {ex.Message}");
					}

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

		protected override void HandleSetup(RSocketProtocol.Setup message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			if (message.KeepAlive > 0 && message.Lifetime > 0)
			{
				this.StartKeepAlive(TimeSpan.FromMilliseconds(message.KeepAlive), TimeSpan.FromMilliseconds(message.Lifetime));
				this.SendKeepAlive(0, false);
			}
		}
	}
}
