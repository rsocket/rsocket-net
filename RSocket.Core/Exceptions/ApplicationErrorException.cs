using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class ApplicationErrorException : RSocketErrorException
	{
		public ApplicationErrorException(string message, int streamId) : base(message, streamId)
		{
		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Application_Error;
	}
}
