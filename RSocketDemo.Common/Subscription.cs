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
		public int _requests;
		public int _resposes;
		public int _maxResponses = int.MaxValue;
		public int GenDataTimeInterval = 0;

		public List<int> _requestNList = new List<int>();
		bool _disposed;

		BlockingCollection<int> _requestNs = new BlockingCollection<int>();

		public event Action<Subscription> OnDisposing;

		public Subscription(IObserver<Payload> observer)
		{
			this._observer = observer;
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this._requestNs.Add(-1);
			this.Dispose(true);
			this._disposed = true;
		}

		protected virtual void Dispose(bool disposing)
		{
			var onDisposing = this.OnDisposing;
			if (onDisposing != null)
				onDisposing(this);
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

					if (requestN < 0)
						break;

					for (int i = 0; i < requestN; i++)
					{
						if (this._disposed)
							return;

						try
						{
							var payload = this.GenPayload(this._resposes + 1);
							this._observer.OnNext(payload);
							Thread.Sleep(this.GenDataTimeInterval);
						}
						catch (Exception ex)
						{
							this._observer.OnError(ex);
							return;
						}


						this._resposes++;

						if (this._resposes >= this._maxResponses)
						{
							this._observer.OnCompleted();
							return;
						}

						//if ((this._maxResponses - this._resposes) <= 10)
						//{
						//	Thread.Sleep(500);
						//}
					}
				}
			});
		}
	}

}
