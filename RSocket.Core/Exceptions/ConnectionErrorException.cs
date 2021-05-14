using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class ConnectionErrorException : RSocketErrorException
	{
		public ConnectionErrorException(string message) : base(message)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Connection_Error;
	}
}
