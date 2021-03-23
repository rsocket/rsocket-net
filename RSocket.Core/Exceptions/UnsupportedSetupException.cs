using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class UnsupportedSetupException : RSocketErrorException
	{
		public UnsupportedSetupException(string message) : base(message)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Unsupported_Setup;
	}
}
