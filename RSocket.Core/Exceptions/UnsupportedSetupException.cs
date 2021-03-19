using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket.Exceptions
{
	public class UnsupportedSetupException : Exception
	{
		public UnsupportedSetupException(string message) : base(message)
		{

		}
	}
}
