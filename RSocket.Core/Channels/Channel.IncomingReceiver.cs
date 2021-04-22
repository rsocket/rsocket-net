using System;

namespace RSocket.Channels
{
	public abstract partial class Channel
	{
		class IncomingReceiver : Publisher<Payload>, IObserver<Payload>, IDisposable
		{
			public IncomingReceiver()
			{

			}
		}
	}
}
