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
		int _errorTrigger = int.MaxValue;

		public int GenDataTimeInterval = 0;

		public OutputPublisher(RSocket.RSocket socket) : base(socket)
		{

		}
		public OutputPublisher(RSocket.RSocket socket, int maxOutputs, int errorTrigger = int.MaxValue) : base(socket)
		{
			this._maxOutputs = maxOutputs;
			this._errorTrigger = errorTrigger;
		}

		protected override ISubscription DoSubscribe(IObserver<Payload> observer)
		{
			OutputSubscription subscription = new OutputSubscription(this.Socket, observer, this._maxOutputs, this._errorTrigger);
			subscription.GenDataTimeInterval = this.GenDataTimeInterval;
			subscription.Start();
			return subscription;
		}
	}
}
