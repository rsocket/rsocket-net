using RSocket.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	internal static class Helpers
	{
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
						return new ApplicationErrorException(errorText);
					case ErrorCodes.Rejected:
						return new RejectedException(errorText);
					case ErrorCodes.Canceled:
						return new CanceledException(errorText);
					case ErrorCodes.Invalid:
						return new InvalidException(errorText);
					default:
						return new InvalidOperationException($"Invalid Error frame in Stream ID {streamId}: {errorCode} '{errorText}'");
				}
			}
		}

		public static async Task ForEach<TSource>(IAsyncEnumerable<TSource> source, Func<TSource, Task> action, Func<Task> final = default, CancellationToken cancel = default)
		{
			await foreach (var item in source)
			{
				if (cancel.IsCancellationRequested)
					break;

				await action(item);
			}

			if (!cancel.IsCancellationRequested)
				await final?.Invoke();
		}

		public static async IAsyncEnumerable<T> MakeControllableStream<T>(IObservable<T> stream, IObservable<int> requestNObservable)
		{
			int requests = 0;
			int responsed = 0;
			object lockObject = new object();
			int lockNumber = 0;

			IAsyncEnumerator<T> streamEnumerator = null;
			IAsyncEnumerator<int> requestNEnumerator = null;
			try
			{
				requestNEnumerator = requestNObservable.ToAsyncEnumerable().GetAsyncEnumerator();

				while (await requestNEnumerator.MoveNextAsync())
				{
					int requestN = requestNEnumerator.Current;
					lock (lockObject)
					{
						Interlocked.Add(ref requests, requestN);

#if DEBUG
						Console.WriteLine($"requests: {requests}");
#endif

						if (requests < 0)
						{
							requests = int.MaxValue;
						}
					}

					while (GetLock(ref lockNumber, ref requests, ref responsed, lockObject))
					{
						try
						{
#if DEBUG
							Console.WriteLine($"requests: {requests}, responsed {responsed},");
#endif
							if (streamEnumerator == null)
								streamEnumerator = stream.ToAsyncEnumerable().GetAsyncEnumerator();

							var next = await streamEnumerator.MoveNextAsync();

							if (next)
							{
								var nextValue = streamEnumerator.Current;
								yield return nextValue;
								Interlocked.Increment(ref responsed);
							}
							else
							{
								goto BreakRequestNLoop;
							}
						}
						finally
						{
							Interlocked.Exchange(ref lockNumber, 0);
						}
					}

					continue;

				BreakRequestNLoop:
					break;
				}
			}
			finally
			{
				if (streamEnumerator != null)
				{
					await streamEnumerator.DisposeAsync();
#if DEBUG
					Console.WriteLine("await streamEnumerator.DisposeAsync()");
#endif
				}

				if (requestNEnumerator != null)
				{
					await requestNEnumerator.DisposeAsync();
#if DEBUG
					Console.WriteLine("await requestNEnumerator.DisposeAsync()");
#endif
				}
			}
		}

		static bool GetLock(ref int lockNumber, ref int requests, ref int responsed, object lockObject)
		{
			if (Interlocked.Increment(ref lockNumber) != 1)
			{
				return false;
			}

			lock (lockObject)
			{
				if (responsed < requests)
					return true;
			}

			Interlocked.Exchange(ref lockNumber, 0);
			return false;
		}
	}
}
