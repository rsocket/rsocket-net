using RSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace RSocketDemo
{
	/// <summary>
	/// Implement the IPublisher` interface to support backpressure.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class Publisher<T> : IPublisher<T>, IObservable<T>
	{
		protected RSocket.RSocket Socket;

		public Publisher(RSocket.RSocket socket)
		{
			this.Socket = socket;
		}

		IDisposable IObservable<T>.Subscribe(IObserver<T> observer)
		{
			return (this as IPublisher<T>).Subscribe(observer);
		}

		ISubscription IPublisher<T>.Subscribe(IObserver<T> observer)
		{
			return this.DoSubscribe(observer);
		}

		protected virtual ISubscription DoSubscribe(IObserver<T> observer)
		{
			throw new NotImplementedException();
		}
	}
}
