using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class ConnectionCloseException : Exception
	{
		public ConnectionCloseException(string message) : base(message)
		{

		}
	}
}
