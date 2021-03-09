using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	public interface ISubscriber<T> : IObserver<T>
	{
		void OnSubscribe(ISubscription subscription);
	}

	public class Subscriber<T> : ISubscriber<T>
	{
		public ISubscription Subscription { get; set; }

		public virtual void OnCompleted()
		{

		}

		public virtual void OnError(Exception error)
		{

		}

		public virtual void OnNext(T value)
		{

		}

		public virtual void DoOnNext(T value)
		{

		}

		public virtual void OnSubscribe(ISubscription subscription)
		{
			this.Subscription = subscription;
		}
	}
}
