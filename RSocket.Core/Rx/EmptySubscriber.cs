using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	public class EmptySubscriber : IObserver<Payload>
	{
		public static readonly EmptySubscriber Instance = new EmptySubscriber();

		EmptySubscriber()
		{
		}

		public void OnCompleted()
		{

		}

		public void OnError(Exception error)
		{

		}

		public void OnNext(Payload value)
		{

		}
	}
}
