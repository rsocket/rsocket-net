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

		public virtual void OnSubscribe(ISubscription subscription)
		{
			this.Subscription = subscription;
		}
	}

	public class InnerSubscriber<T> : Subscriber<T>
	{
		Action<T> _onNext;
		Action _onCompleted;
		Action<Exception> _onError;
		Action<ISubscription> _onSubscribe;
		public InnerSubscriber(Action<T> onNext, Action<Exception> onError, Action onCompleted, Action<ISubscription> onSubscribe)
		{
			this._onNext = onNext;
			this._onCompleted = onCompleted;
			this._onError = onError;
			this._onSubscribe = onSubscribe;
		}

		public override void OnCompleted()
		{
			base.OnCompleted();
			this._onCompleted();
		}

		public override void OnError(Exception error)
		{
			base.OnError(error);
			this._onError(error);
		}

		public override void OnNext(T value)
		{
			base.OnNext(value);
			this._onNext(value);
		}

		public override void OnSubscribe(ISubscription subscription)
		{
			base.OnSubscribe(subscription);
			this._onSubscribe(subscription);
		}
	}

	public class OutputSubscriber<T> : Subscriber<T>
	{
		IObserver<T> _output;
		int i = 0;
		public OutputSubscriber(IObserver<T> output)
		{
			this._output = output;
		}

		public override void OnCompleted()
		{
			this._output.OnCompleted();
		}

		public override void OnError(Exception error)
		{
			this._output.OnError(error);
		}

		public override void OnNext(T value)
		{
			//Console.WriteLine($"OnNext {i++}");
			this._output.OnNext(value);
		}
	}
}
