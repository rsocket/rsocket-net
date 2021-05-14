using System;
using System.Collections.Generic;
using System.Text;
using static RSocket.RSocketProtocol;

namespace RSocket.Exceptions
{
	public abstract class RSocketErrorException : Exception
	{
		protected RSocketErrorException(string message) : base(message)
		{

		}
		protected RSocketErrorException(string message, int streamId) : this(message)
		{
			this.StreamId = streamId;
		}

		public abstract ErrorCodes ErrorCode { get; }

		public int StreamId { get; private set; }
	}
}
