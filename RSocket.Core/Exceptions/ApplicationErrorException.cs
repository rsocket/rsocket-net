using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class ApplicationErrorException : Exception
	{
		public ApplicationErrorException(string message) : base(message)
		{

		}
	}
}
