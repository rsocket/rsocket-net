using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RSocket
{
	class IncomingReceiver : SimplePublisher<Payload>, IObservable<Payload>, IDisposable
	{
	}
}
