using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class RejectedSetupException : RSocketErrorException
	{
		public RejectedSetupException(string message) : base(message)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Rejected_Setup;
	}
}
