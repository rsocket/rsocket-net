using RSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace RSocketDemo
{
	/// <summary>
	/// Implement the IPublisher` interface to support backpressure.
	/// </summary>
	public class OutputPublisher : Publisher<Payload>, IPublisher<Payload>, IObservable<Payload>
	{
		int _maxOutputs = int.MaxValue;

		public OutputPublisher(RSocket.RSocket socket) : base(socket)
		{

		}
		public OutputPublisher(RSocket.RSocket socket, int maxOutputs) : base(socket)
		{
			this._maxOutputs = maxOutputs;
		}

		protected override ISubscription DoSubscribe(IObserver<Payload> observer)
		{
			OutputSubscription subscription = new OutputSubscription(this.Socket, observer, this._maxOutputs);
			subscription.Start();
			return subscription;
		}
	}
}
