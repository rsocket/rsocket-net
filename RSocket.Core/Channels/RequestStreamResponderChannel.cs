using System.Buffers;
using System.Threading.Tasks;
using Channeler = System.Func<(System.Buffers.ReadOnlySequence<byte> Data, System.Buffers.ReadOnlySequence<byte> Metadata), System.IObservable<RSocket.Payload>, System.IObservable<RSocket.Payload>>;

namespace RSocket
{
	public class RequestStreamResponderChannel : ResponderChannel
	{
		public RequestStreamResponderChannel(RSocket socket, int channelId, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data, int initialRequest, Channeler channeler) : base(socket, channelId, metadata, data, initialRequest, channeler)
		{

		}

		public override async Task ToTask()
		{
			this.FinishIncoming();
			await base.ToTask();
		}
	}
}
