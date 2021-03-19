using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class InvalidSetupException : Exception
	{
		public InvalidSetupException(string message) : base(message)
		{

		}
	}
}
