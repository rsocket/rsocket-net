using System;
using System.Collections.Generic;
using System.Text;
using static RSocket.RSocketProtocol;

namespace RSocket.Exceptions
{
	public abstract class RSocketErrorException : Exception
	{
		protected RSocketErrorException(string message) : base(message)
		{

		}

		public abstract ErrorCodes ErrorCode { get; }
	}
}
