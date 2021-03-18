using RSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace RSocketDemo
{
	public class OutputSubscription : Subscription
	{
		RSocket.RSocket Socket;

		public OutputSubscription(RSocket.RSocket socket, IObserver<Payload> observer,  int maxResponses = int.MaxValue) : base(observer)
		{
			this.Socket = socket;
			this._maxResponses = maxResponses;
		}

		protected override Payload GenPayload(int i)
		{
			return new Payload($"data-{i}".ToReadOnlySequence(), $"metadata-{i}".ToReadOnlySequence());
		}
	}

}
