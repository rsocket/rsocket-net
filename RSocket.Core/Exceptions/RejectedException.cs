using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class RejectedException : Exception
	{
		public RejectedException(string message) : base(message)
		{

		}
	}
}
