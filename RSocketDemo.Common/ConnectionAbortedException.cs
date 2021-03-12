using System;
using System.Collections.Generic;
using System.Text;

namespace RSocketDemo
{
	public class ConnectionAbortedException : OperationCanceledException
	{
		public ConnectionAbortedException()
		{
		}
		public ConnectionAbortedException(string message) : base(message)
		{
		}
		public ConnectionAbortedException(string message, Exception inner) : base(message, inner)
		{

		}
	}
}
