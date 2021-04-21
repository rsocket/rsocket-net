using System;

namespace RSocket.Channels
{
	public abstract partial class Channel
	{
		class IncomingReceiver : SimplePublisher<Payload>, IObservable<Payload>, IDisposable
		{

		}
	}
}
