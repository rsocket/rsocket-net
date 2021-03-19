using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class ConnectionErrorException : Exception
	{
		public ConnectionErrorException(string message) : base(message)
		{

		}
	}
}
