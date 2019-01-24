using System;
using System.Buffers;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RSocket.RPC
{
	public abstract class RSocketService<T> : IRSocketStream
	{
		private readonly RSocketClient Client;

		public RSocketService(RSocketClient client) { Client = client; }

		protected void __RequestFireAndForget(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestFireAndForget(null, data, metadata); }



		protected async Task<TResult> __RequestResponse<TResult>(string service, string method, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default)
		{
			var receiver = new Receiver<TResult>();
			await Client.RequestResponse(receiver, data, new RemoteProcedureCall.RemoteProcedureCallMetadata(service, method, metadata, tracing));
			return await receiver.Task.ConfigureAwait(false);
		}

		//TODO Ask about semantics of this - should it execute the server call before subscription?

		protected async Task<TResult> __RequestStream<TResult>(string service, string method, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default)
		{
			var receiver = new Receiver<TResult>();
			await Client.RequestStream(receiver, data, new RemoteProcedureCall.RemoteProcedureCallMetadata(service, method, metadata, tracing), initial: 3);
			return await receiver.Task.ConfigureAwait(false);
		}


		//protected void RequestStream(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestStream(null, data, metadata); }   //TODO Initial? Or get from policy?
		//protected void RequestChannel(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestChannel(null, data, metadata); } //TODO Initial?


		public void Dispatch()
		{
		}

		//If a Completion arrives before the first dispatch, forward directly.
		void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnCompleted()
		{
			throw new NotImplementedException();
		}

		void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnError(Exception error)
		{
			throw new NotImplementedException();
		}

		void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnNext((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value)
		{
			throw new NotImplementedException();
		}

		private class Receiver<TResult> : TaskCompletionSource<TResult>, IRSocketStream
		{
			public Receiver() { }

			public void OnCompleted() { }
			public void OnError(Exception error) => base.SetException(error);
			public void OnNext((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value)
			{
				Console.WriteLine($"OnNext: [{value.metadata.Length}]:[{value.data.Length}]");
			}
		}

		private Lazy<List<System.Reflection.MethodInfo>> Methods = new Lazy<List<System.Reflection.MethodInfo>>(() => GetMethods());

		static public List<System.Reflection.MethodInfo> GetMethods() => (
			from method in typeof(T).GetMethods()
			where method.IsPublic && method.DeclaringType == typeof(T)
			select method).ToList();
	}
}
