using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class InvalidSetupException : RSocketErrorException
	{
		public InvalidSetupException(string message) : base(message)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Invalid_Setup;
	}
}
