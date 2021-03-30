using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RSocket
{
	class IncomingReceiver : SimplePublisher<Payload>, IObservable<Payload>, IDisposable
	{
		public IncomingReceiver(FrameHandler frameHandler)
		{
			IncomingStreamSubscriber subscriber = new IncomingStreamSubscriber(EmptySubscriber<Payload>.Instance, frameHandler);
			subscriber.Subscribe(this); //In case the application layer does not subscribe incoming that the incoming status is unfinished always.
		}
	}
}
