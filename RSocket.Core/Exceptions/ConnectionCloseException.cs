using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class ConnectionCloseException : RSocketErrorException
	{
		public ConnectionCloseException(string message) : base(message)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Connection_Close;
	}
}
