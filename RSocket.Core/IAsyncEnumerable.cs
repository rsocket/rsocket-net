#region Assembly System.Runtime, Version=4.2.1.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// .nuget\packages\microsoft.netcore.app\3.0.0-preview-27324-5\ref\netcoreapp3.0\System.Runtime.dll
#endregion

using System.Threading;
using System.Threading.Tasks;

namespace RSocket.Collections.Generic
{
	public interface IAsyncEnumerable<out T>
	{
		IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);
	}

	public interface IAsyncEnumerator<out T> : IAsyncDisposable
	{
		T Current { get; }
		ValueTask<bool> MoveNextAsync();
	}

	public interface IAsyncDisposable
	{
		ValueTask DisposeAsync();
	}
}
