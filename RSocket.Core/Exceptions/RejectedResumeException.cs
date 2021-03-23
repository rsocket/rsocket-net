using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class RejectedResumeException : RSocketErrorException
	{
		public RejectedResumeException(string message) : base(message)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Rejected_Resume;
	}
}
