using RSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace RSocketDemo
{
	public class OutputSubscription : Subscription
	{
		RSocket.RSocket Socket;

		int _errorTrigger = int.MaxValue;


		public OutputSubscription(RSocket.RSocket socket, IObserver<Payload> observer, int maxResponses = int.MaxValue, int errorTrigger = int.MaxValue) : base(observer)
		{
			this.Socket = socket;
			this._maxResponses = maxResponses;
			this._errorTrigger = errorTrigger;
		}

		protected override Payload GenPayload(int i)
		{
			if (this._errorTrigger <= i)
			{
				throw new Exception("This is a test error.");
			}

			return new Payload($"data-{i}".ToReadOnlySequence(), $"metadata-{i}".ToReadOnlySequence());
		}
	}

}
