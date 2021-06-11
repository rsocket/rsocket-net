using System;
using System.Runtime.Serialization;

namespace RSocket
{
	public class RSocketException : Exception
	{
		public RSocketProtocol.ErrorCodes Error { get; }

		public RSocketException(RSocketProtocol.ErrorCodes error)
		{
			Error = error;
		}

		protected RSocketException(SerializationInfo info, StreamingContext context, RSocketProtocol.ErrorCodes error) : base(info, context)
		{
			Error = error;
		}

		public RSocketException(string message, RSocketProtocol.ErrorCodes error) : base(message)
		{
			Error = error;
		}

		public RSocketException(string message, Exception innerException, RSocketProtocol.ErrorCodes error) : base(message, innerException)
		{
			Error = error;
		}
	}
}
