using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	public interface ISubscription : IDisposable
	{
		void Request(int n);
	}
}
