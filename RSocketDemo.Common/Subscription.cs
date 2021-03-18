using RSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public class Subscription : ISubscription
	{
		IObserver<Payload> _observer;
		protected int _requests;
		protected int _resposes;
		protected int _maxResponses = int.MaxValue;

		protected List<int> _requestNList = new List<int>();
		bool _disposed;

		BlockingCollection<int> _requestNs = new BlockingCollection<int>();

		public Subscription(IObserver<Payload> observer)
		{
			this._observer = observer;
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this.Dispose(true);
			this._disposed = true;
		}

		protected virtual void Dispose(bool disposing)
		{

		}

		public void Request(int n)
		{
			if (this._disposed)
				return;

			Interlocked.Add(ref this._requests, n);
			this._requestNList.Add(n);
			this._requestNs.Add(n);
		}

		protected virtual Payload GenPayload(int i)
		{
			return default(Payload);
		}

		public void Start()
		{
			//Simulation to generate data.
			Task.Run(() =>
			{
				while (true)
				{
					int requestN = this._requestNs.Take();

					for (int i = 0; i < requestN; i++)
					{
						if (this._disposed)
							return;

						this._observer.OnNext(this.GenPayload(this._resposes + 1));

						this._resposes++;

						if (this._resposes >= this._maxResponses)
						{
							this._observer.OnCompleted();
							return;
						}

						if ((this._maxResponses - this._resposes) <= 10)
						{
							Thread.Sleep(500);
						}
					}
				}
			});
		}
	}

}
