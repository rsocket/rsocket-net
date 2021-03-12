using RSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RSocketDemo
{
	public class SubscriberBase<T> : Subscriber<T>
	{
		int _requests = 0;
		int _responsed = 0;

		public int RequestSize { get; set; }

		public SubscriberBase(int requestSize)
		{
			this.RequestSize = requestSize;
			this._requests = requestSize;
		}

		public override void OnCompleted()
		{
			base.OnCompleted();
		}

		public override void OnError(Exception error)
		{
			base.OnError(error);
		}

		public override void OnNext(T value)
		{
			try
			{
				this.DoOnNext(value);
				this._responsed++;

				if (this._responsed == this._requests)
				{
					Random random = new Random();
					int requestN = random.Next(1, 3);
					//requestN = int.MaxValue;
					requestN = 1;
					this._requests = this._requests + requestN;
					this.Subscription.Request(requestN);
					//Console.WriteLine($"this.Subscription.Request {Thread.CurrentThread.ManagedThreadId}");
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
