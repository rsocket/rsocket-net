using System;

namespace RSocket
{
	public interface ISubscription : IDisposable
	{
		void Request(int n);
	}
}
