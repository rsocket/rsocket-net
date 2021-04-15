using System;

namespace RSocket
{
	public class EmptySubscriber<T> : IObserver<T>
	{
		public static readonly EmptySubscriber<T> Instance = new EmptySubscriber<T>();

		EmptySubscriber()
		{
		}

		public void OnCompleted()
		{

		}

		public void OnError(Exception error)
		{

		}

		public void OnNext(T value)
		{

		}
	}
}
