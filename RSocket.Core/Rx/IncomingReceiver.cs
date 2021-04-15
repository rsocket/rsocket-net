using RSocket.Channels;
using System;

namespace RSocket
{
	class IncomingReceiver : SimplePublisher<Payload>, IObservable<Payload>, IDisposable
	{
		public IncomingReceiver(Channel channel)
		{
			IncomingStreamSubscriber subscriber = new IncomingStreamSubscriber(EmptySubscriber<Payload>.Instance, channel);
			subscriber.Subscribe(this); //In case the application layer does not subscribe incoming that the incoming status is unfinished always.
		}
	}
}
