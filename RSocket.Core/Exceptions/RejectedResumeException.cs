using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class RejectedResumeException : Exception
	{
		public RejectedResumeException(string message) : base(message)
		{

		}
	}
}
