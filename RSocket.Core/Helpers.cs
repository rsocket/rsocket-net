using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	internal static class Helpers
	{
		public static async Task ForEach<TSource>(IAsyncEnumerable<TSource> source, Func<TSource, Task> action, CancellationToken cancel = default, Func<Task> final = default)
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
						Console.WriteLine($"requests: {requests}");
						if (requests < 0)
						{
							requests = int.MaxValue;
						}
					}

					while (GetLock(ref lockNumber, ref requests, ref responsed, lockObject))
					{
						try
						{
							Console.WriteLine($"requests: {requests}, responsed {responsed},");
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
					Console.WriteLine("await requestNEnumerator.DisposeAsync()");
				}

				if (requestNEnumerator != null)
				{
					await requestNEnumerator.DisposeAsync();
					Console.WriteLine("await requestNEnumerator.DisposeAsync()");
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
