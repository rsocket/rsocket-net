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
			try
			{
				this.DoOnCompleted();
			}
			catch
			{
			}
		}
		protected virtual void DoOnCompleted()
		{
		}

		public void OnError(Exception error)
		{
			if (this._completed)
				return;

			this._error = error;
			this.Finished();
			try
			{
				this.DoOnError(error);
			}
			catch
			{
			}
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
