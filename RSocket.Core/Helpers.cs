using RSocket.Exceptions;
using System;
using System.Buffers;
using System.Linq;
using System.Text;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	internal static class Helpers
	{
		public static ReadOnlySequence<byte> Clone(this ReadOnlySequence<byte> sequence)
		{
			return new ReadOnlySequence<byte>(sequence.ToArray());
		}

		public static ReadOnlySequence<byte> StringToByteSequence(string s)
		{
			if (s == null)
				return default(ReadOnlySequence<byte>);

			var ret = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(s));
			return ret;
		}

		public static IPublisher<T> AsPublisher<T>(IObservable<T> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			var pub = source as IPublisher<T>;
			if (pub == null)
				pub = new PublisherAdapter<T>(source);

			return pub;
		}

		public static Exception MakeException(this Error error)
		{
			int streamId = error.Stream;
			ErrorCodes errorCode = error.ErrorCode;
			string errorText = error.ErrorText;

			if (streamId == 0)
			{
				switch (errorCode)
				{
					case ErrorCodes.Invalid_Setup:
						return new InvalidSetupException(errorText);
					case ErrorCodes.Unsupported_Setup:
						return new UnsupportedSetupException(errorText);
					case ErrorCodes.Rejected_Setup:
						return new RejectedSetupException(errorText);
					case ErrorCodes.Rejected_Resume:
						return new RejectedResumeException(errorText);
					case ErrorCodes.Connection_Error:
						return new ConnectionErrorException(errorText);
					case ErrorCodes.Connection_Close:
						return new ConnectionCloseException(errorText);
					default:
						return new InvalidOperationException($"Invalid Error frame in Stream ID 0: {errorCode} '{errorText}'");
				}
			}
			else
			{
				switch (errorCode)
				{
					case ErrorCodes.Application_Error:
						return new ApplicationErrorException(errorText, streamId);
					case ErrorCodes.Rejected:
						return new RejectedException(errorText, streamId);
					case ErrorCodes.Canceled:
						return new CanceledException(errorText, streamId);
					case ErrorCodes.Invalid:
						return new InvalidException(errorText, streamId);
					default:
						return new InvalidOperationException($"Invalid Error frame in Stream ID {streamId}: {errorCode} '{errorText}'");
				}
			}
		}
	}
}
