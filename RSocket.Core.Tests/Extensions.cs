using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RSocket.Tests
{
	static public class Extensions
	{
		static public IAsyncEnumerable<T> ToAsyncEnumerable<T>(this System.Collections.Async.IAsyncEnumerable<T> source) => AsyncEnumerable.Create(cancel =>
		{
			System.Collections.Async.IAsyncEnumerator<T> enumerator = default;
			return AsyncEnumerator.Create(async () =>
			{
				if (enumerator == default) { enumerator = await source.GetAsyncEnumeratorAsync(cancel); }
				return await enumerator.MoveNextAsync();
			}, () => enumerator.Current, () => { enumerator.Dispose(); return new ValueTask(); });
		});
	}
}
