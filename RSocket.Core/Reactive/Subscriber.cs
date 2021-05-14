using System;
using System.Threading;

namespace RSocket
{
	public class Subscriber<T> : IObserver<T>
	{
		bool _completed;
		Exception _error;

		public bool IsCompleted { get { return this._completed; } }
		public Exception Error { get { return this._error; } }

		void Finished()
		{
			this._completed = true;
		}

		public void OnCompleted()
		{
			if (this._completed)
				return;

			this.Finished();
			this.DoOnCompleted();
		}
		protected virtual void DoOnCompleted()
		{
		}

		public void OnError(Exception error)
		{
			if (this._completed)
				return;

			this.Finished();
			this._error = error;
			this.DoOnError(error);
		}

		protected virtual void DoOnError(Exception error)
		{
		}

		public void OnNext(T value)
		{
			if (this._completed)
				return;

			try
			{
				this.DoOnNext(value);
			}
			catch
			{
				this.Finished();
				throw;
			}
		}
		protected virtual void DoOnNext(T value)
		{
		}
	}
}
