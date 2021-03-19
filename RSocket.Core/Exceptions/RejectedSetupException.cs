using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class RejectedSetupException : Exception
	{
		public RejectedSetupException(string message) : base(message)
		{

		}
	}
}
