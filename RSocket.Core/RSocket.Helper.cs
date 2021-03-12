using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using static RSocket.RSocketProtocol;
using IRSocketStream = System.IObserver<(System.Buffers.ReadOnlySequence<byte> metadata, System.Buffers.ReadOnlySequence<byte> data)>;

namespace RSocket
{
	partial class RSocket
	{
		//internal static async IAsyncEnumerable<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> MakeControllablePayloads(IObservable<int> requestNObservable, IAsyncEnumerable<(ReadOnlySequence<byte>, ReadOnlySequence<byte>)> payloads, [EnumeratorCancellationAttribute] CancellationToken cancel = default)
		//{
		//	int requests = 0;
		//	int responsed = 0;
		//	object lockObject = new object();
		//	int lockNumber = 0;

		//	IAsyncEnumerator<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> payloadsEnumerator = null;
		//	IAsyncEnumerator<int> requestNEnumerator = null;
		//	try
		//	{
		//		requestNEnumerator = requestNObservable.ToAsyncEnumerable().GetAsyncEnumerator();

		//		while (await requestNEnumerator.MoveNextAsync())
		//		{
		//			int requestN = requestNEnumerator.Current;
		//			lock (lockObject)
		//			{
		//				Interlocked.Add(ref requests, requestN);
		//				Console.WriteLine(requests);
		//				if (requests <= 0)
		//				{
		//					requests = int.MaxValue;
		//				}
		//			}

		//			while (GetLock(ref lockNumber, ref requests, ref responsed, lockObject))
		//			{
		//				try
		//				{
		//					Console.WriteLine(cancel.IsCancellationRequested);
		//					if (cancel.IsCancellationRequested)
		//					{
		//						//Console.WriteLine(cancel.IsCancellationRequested);
		//						goto BreakRequestNLoop;
		//					}

		//					if (payloadsEnumerator == null)
		//						payloadsEnumerator = payloads.GetAsyncEnumerator();

		//					var next = await payloadsEnumerator.MoveNextAsync();

		//					if (next)
		//					{
		//						var nextValue = payloadsEnumerator.Current;
		//						yield return nextValue;
		//						Interlocked.Increment(ref responsed);
		//					}
		//					else
		//					{
		//						goto BreakRequestNLoop;
		//					}
		//				}
		//				finally
		//				{
		//					Interlocked.Exchange(ref lockNumber, 0);
		//				}
		//			}

		//			continue;

		//		BreakRequestNLoop:
		//			break;
		//		}
		//	}
		//	finally
		//	{
		//		if (payloadsEnumerator != null)
		//		{
		//			await payloadsEnumerator.DisposeAsync();
		//			Console.WriteLine("await requestNEnumerator.DisposeAsync()");
		//		}

		//		if (requestNEnumerator != null)
		//		{
		//			await requestNEnumerator.DisposeAsync();
		//			Console.WriteLine("await requestNEnumerator.DisposeAsync()");
		//		}
		//	}
		//}

		//internal static async IAsyncEnumerable<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> MakeControllableStream(IObservable<(ReadOnlySequence<byte>, ReadOnlySequence<byte>)> stream, IObservable<int> requestNObservable)
		//{
		//	int requests = 0;
		//	int responsed = 0;
		//	object lockObject = new object();
		//	int lockNumber = 0;

		//	IAsyncEnumerator<(ReadOnlySequence<byte>, ReadOnlySequence<byte>)> streamEnumerator = null;
		//	IAsyncEnumerator<int> requestNEnumerator = null;
		//	try
		//	{
		//		requestNEnumerator = requestNObservable.ToAsyncEnumerable().GetAsyncEnumerator();

		//		while (await requestNEnumerator.MoveNextAsync())
		//		{
		//			int requestN = requestNEnumerator.Current;
		//			lock (lockObject)
		//			{
		//				Interlocked.Add(ref requests, requestN);
		//				Console.WriteLine($"requests: {requests}");
		//				if (requests <= 0)
		//				{
		//					requests = int.MaxValue;
		//				}
		//			}

		//			while (GetLock(ref lockNumber, ref requests, ref responsed, lockObject))
		//			{
		//				try
		//				{
		//					Console.WriteLine($"requests: {requests}, responsed {responsed},");
		//					if (streamEnumerator == null)
		//						streamEnumerator = stream.ToAsyncEnumerable().GetAsyncEnumerator();

		//					var next = await streamEnumerator.MoveNextAsync();

		//					if (next)
		//					{
		//						var nextValue = streamEnumerator.Current;
		//						yield return nextValue;
		//						Interlocked.Increment(ref responsed);
		//					}
		//					else
		//					{
		//						goto BreakRequestNLoop;
		//					}
		//				}
		//				finally
		//				{
		//					Interlocked.Exchange(ref lockNumber, 0);
		//				}
		//			}

		//			continue;

		//		BreakRequestNLoop:
		//			break;
		//		}
		//	}
		//	finally
		//	{
		//		if (streamEnumerator != null)
		//		{
		//			await streamEnumerator.DisposeAsync();
		//			Console.WriteLine("await requestNEnumerator.DisposeAsync()");
		//		}

		//		if (requestNEnumerator != null)
		//		{
		//			await requestNEnumerator.DisposeAsync();
		//			Console.WriteLine("await requestNEnumerator.DisposeAsync()");
		//		}
		//	}
		//}

		internal static async IAsyncEnumerable<T> MakeControllableStream<T>(IObservable<T> stream, IObservable<int> requestNObservable)
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
						if (requests <= 0)
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
