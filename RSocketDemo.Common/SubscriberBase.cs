using RSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RSocketDemo
{
	public class SubscriberBase<T> : IObserver<T>
	{
		int _requests = 0;
		int _responsed = 0;

		public int RequestSize { get; set; }

		public ISubscription Subscription { get; set; }

		public SubscriberBase(int requestSize)
		{
			this.RequestSize = requestSize;
			this._requests = requestSize;
		}

		public virtual void OnCompleted()
		{

		}

		public virtual void OnError(Exception error)
		{

		}

		public virtual void OnNext(T value)
		{
			try
			{
				this.DoOnNext(value);
				this._responsed++;

				if (this._responsed == this._requests)
				{
					Random random = new Random();
					int requestN = random.Next(1, 5);
					//requestN = int.MaxValue;
					//requestN = 1;
					this._requests = this._requests + requestN;
					this.Subscription?.Request(requestN);
				}
			}
			finally
			{

			}
		}

		public virtual void DoOnNext(T value)
		{

		}
	}

}
