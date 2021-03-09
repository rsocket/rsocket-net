using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using static RSocket.RSocketProtocol;
using IRSocketStream = System.IObserver<(System.Buffers.ReadOnlySequence<byte> metadata, System.Buffers.ReadOnlySequence<byte> data)>;

namespace RSocket
{
	partial class RSocket
	{
		static async IAsyncEnumerable<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> MakeControllablePayloads(IObservable<int> requestNObservable, IAsyncEnumerable<(ReadOnlySequence<byte>, ReadOnlySequence<byte>)> payloads)
		{
			int requests = 0;
			int responsed = 0;
			object lockObject = new object();
			int lockNumber = 0;

			IAsyncEnumerator<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> enumerator = null;

			try
			{
				await foreach (int requestN in requestNObservable.ToAsyncEnumerable())
				{
					lock (lockObject)
					{
						Interlocked.Add(ref requests, requestN);
					}

					while (GetLock(ref lockNumber, ref requests, ref responsed, lockObject))
					{
						try
						{
							if (enumerator == null)
								enumerator = payloads.GetAsyncEnumerator();

							var next = await enumerator.MoveNextAsync();

							if (next)
							{
								var nextValue = enumerator.Current;
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
				if (enumerator != null)
					await enumerator.DisposeAsync();
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
