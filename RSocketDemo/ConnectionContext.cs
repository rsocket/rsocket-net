using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public abstract class ConnectionContext : BaseConnectionContext, IAsyncDisposable
	{
		/// <summary>
		/// Gets or sets the <see cref="IDuplexPipe"/> that can be used to read or write data on this connection.
		/// </summary>
		public abstract IDuplexPipe Transport { get; set; }

		/// <summary>
		/// Aborts the underlying connection.
		/// </summary>
		/// <param name="abortReason">An optional <see cref="ConnectionAbortedException"/> describing the reason the connection is being terminated.</param>
		public override void Abort(ConnectionAbortedException abortReason)
		{
			//// We expect this to be overridden, but this helps maintain back compat
			//// with implementations of ConnectionContext that predate the addition of
			//// ConnectionContext.Abort()
			//Features.Get<IConnectionLifetimeFeature>()?.Abort();
		}

		/// <summary>
		/// Aborts the underlying connection.
		/// </summary>
		public override void Abort() => Abort(new ConnectionAbortedException("The connection was aborted by the application via ConnectionContext.Abort()."));

		public override ValueTask DisposeAsync()
		{
			return default;
		}
	}
}
