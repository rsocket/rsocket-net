using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class ApplicationErrorException : RSocketErrorException
	{
		public ApplicationErrorException(string message) : base(message)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Application_Error;
	}
}
