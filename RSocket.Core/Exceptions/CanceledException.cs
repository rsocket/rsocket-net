using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class CanceledException : RSocketErrorException
	{
		public CanceledException(string message, int streamId) : base(message, streamId)
		{

		}

		public override RSocketProtocol.ErrorCodes ErrorCode => RSocketProtocol.ErrorCodes.Canceled;
	}
}
