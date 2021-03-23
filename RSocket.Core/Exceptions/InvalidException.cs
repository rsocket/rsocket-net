using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class InvalidException : RSocketErrorException
	{
		public InvalidException(string message) : base(message)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Invalid;
	}
}
