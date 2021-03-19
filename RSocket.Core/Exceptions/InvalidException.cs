using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class InvalidException : Exception
	{
		public InvalidException(string message) : base(message)
		{

		}
	}
}
