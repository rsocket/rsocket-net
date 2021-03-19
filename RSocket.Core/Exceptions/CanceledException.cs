using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class CanceledException : Exception
	{
		public CanceledException(string message) : base(message)
		{

		}
	}
}
