//#region Assembly System.Runtime, Version=4.2.1.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
//// .nuget\packages\microsoft.netcore.app\3.0.0-preview-27324-5\ref\netcoreapp3.0\System.Runtime.dll
//#endregion
//using System.Threading;
//using System.Threading.Tasks;
//using System.Collections.Generic;

//namespace RSocket.Collections.Generic
//{
//	public interface IAsyncEnumerable<out T>
//	{
//		IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);
//	}

//	public interface IAsyncEnumerator<out T> : IAsyncDisposable
//	{
//		T Current { get; }
//		ValueTask<bool> MoveNextAsync();
//	}

//	public interface IAsyncDisposable
//	{
//		ValueTask DisposeAsync();
//	}



//	static public class IAsyncEnumerableExtensions
//	{
//		static public IEnumerable<T> ToEnumerable<T>(this IAsyncEnumerable<T> source)
//		{
//			var enumerator = source.GetAsyncEnumerator();
//			try { while (enumerator.MoveNextAsync().Result) { yield return enumerator.Current; } }
//			finally { enumerator.DisposeAsync().AsTask().Wait(); }
//		}


//		static public IAsyncEnumerable<T> AsyncEnumerate<T>(this IEnumerable<Task<T>> source) => new AsyncEnumerable<T>(source);

//		private class AsyncEnumerable<T> : IAsyncEnumerable<T>
//		{
//			readonly IEnumerable<Task<T>> Enumerable;

//			public AsyncEnumerable(IEnumerable<Task<T>> enumerable) { Enumerable = enumerable; }

//			public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new AsyncTaskEnumerator(Enumerable.GetEnumerator());

//			private class AsyncTaskEnumerator : IAsyncEnumerator<T>
//			{
//				readonly IEnumerator<Task<T>> Enumerator;
//				public T Current { get; private set; }

//				public AsyncTaskEnumerator(IEnumerator<Task<T>> enumerator) { Enumerator = enumerator; }

//				public async ValueTask<bool> MoveNextAsync()
//				{
//					while (Enumerator.MoveNext())
//					{
//						Current = await Enumerator.Current;
//						return true;
//					}
//					return false;
//				}

//				public ValueTask DisposeAsync() { Enumerator.Dispose(); return new ValueTask(); }
//			}





//			//		//readonly Func<IRSocketStream, Task> Subscriber;
//			//		//readonly Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), T> Mapper;

//			//		//public Receiver(Func<IRSocketStream, Task> subscriber, Func<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata), T> mapper)
//			//		//{
//			//		//	Subscriber = subscriber;
//			//		//	Mapper = mapper;
//			//		//}

//			//		public async Task<T> ExecuteAsync(CancellationToken cancellation = default)
//			//		{
//			//			var receiver = new Receiver();
//			//			await Subscriber(receiver);
//			//			var result = await receiver.Awaitable;
//			//			return Mapper((result.data, result.metadata));
//			//		}

//			//		public async Task<T> ExecuteAsync(T result, CancellationToken cancellation = default)
//			//		{
//			//			var receiver = new Receiver();
//			//			await Subscriber(receiver);
//			//			return result;
//			//		}

//			//		public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellation = default)
//			//		{
//			//			var enumerator = new MappedEnumerator(Mapper);
//			//			Subscriber(enumerator);     //TODO Do we want to use this task too? It could fault. Also, cancellation. Nope, this should only await on the first MoveNext, so subscription is lower.
//			//			return enumerator;
//			//		}

//			//		private class Enumerator : IAsyncEnumerator<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>, IRSocketStream
//			//		{
//			//			public bool IsCompleted { get; private set; } = false;
//			//			private (ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) Value = default;
//			//			private ConcurrentQueue<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)> Queue;
//			//			private AsyncManualResetEvent Continue = new AsyncManualResetEvent();
//			//			private Exception Error;

//			//			public Enumerator()
//			//			{
//			//				Queue = new ConcurrentQueue<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>();
//			//			}

//			//			public async ValueTask<bool> MoveNextAsync()
//			//			{
//			//				while (true)
//			//				{
//			//					if (Queue.TryDequeue(out Value)) { return true; }
//			//					await Continue.WaitAsync();
//			//					if (Error != default) { throw Error; }
//			//					else if (IsCompleted) { return false; }
//			//					else { Continue.Reset(); }
//			//				}
//			//			}

//			//			public (ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) Current => Value;

//			//			public ValueTask DisposeAsync()
//			//			{
//			//				return new ValueTask();
//			//			}

//			//			public void OnCompleted() { IsCompleted = true; ; Continue.Set(); }
//			//			public void OnError(Exception error) { Error = error; Continue.Set(); }
//			//			public void OnNext((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value)
//			//			{
//			//				//TODO Would we really need to interlock this? If the Queue isn't allocated, it's the first time through...?
//			//				//var value = Interlocked.Exchange<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>(ref Value, default);     //TODO Hmmm, no ValueTuples... Could save Queue allocation if only going to get one...
//			//				Queue.Enqueue(value);
//			//				Continue.Set();
//			//			}

//			//			class AsyncManualResetEvent      //Steven Toub: https://blogs.msdn.microsoft.com/pfxteam/2012/02/11/building-async-coordination-primitives-part-1-asyncmanualresetevent/
//			//			{
//			//				private volatile TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>();
//			//				public Task WaitAsync() => Completion.Task;
//			//				public void Set() { Completion.TrySetResult(true); }
//			//				public void Reset() { while (true) { var previous = Completion; if (!previous.Task.IsCompleted || Interlocked.CompareExchange(ref Completion, new TaskCompletionSource<bool>(), previous) == previous) { return; } } }
//			//			}
//			//		}

//			//		private class MappedEnumerator : Enumerator, IAsyncEnumerator<T>, IRSocketStream
//			//		{
//			//			readonly Func<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data), T> Mapper;

//			//			public MappedEnumerator(Func<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data), T> mapper)
//			//			{
//			//				Mapper = mapper;
//			//			}

//			//			public new T Current => Mapper(base.Current);
//			//			//ReadOnlySequence<byte> IAsyncEnumerator<ReadOnlySequence<byte>>.Current => Value.data;

//			//			public new ValueTask DisposeAsync()
//			//			{
//			//				return base.DisposeAsync();
//			//			}
//			//		}
//		}
//	}
//}
