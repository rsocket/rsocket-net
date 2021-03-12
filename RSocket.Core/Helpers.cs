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
			//await source.ForEachAwaitAsync(async item =>
			//{
			//	await action(item);
			//}, cancel);

			await foreach (var item in source)
			{
				await action(item);
			}

			if (!cancel.IsCancellationRequested)
				await final?.Invoke();
		}
	}
}
