using System;
using System.Buffers;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace RSocket.RPC
{
	public abstract class RSocketService<T> : IRSocketStream
	{
		private readonly RSocketClient Client;

		public RSocketService(RSocketClient client) { Client = client; }

		protected void __RequestFireAndForget(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestFireAndForget(null, data, metadata); }

		protected async Task<TResult> __RequestFireAndForget<TMessage, TResult>(string service, string method, TMessage message, Func<TMessage, byte[]> intransform, Func<byte[], TResult> outtransform, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default) =>
			outtransform((await __RequestFireAndForget(service, method, new ReadOnlySequence<byte>(intransform(message)), metadata, tracing)).ToArray());

		protected async Task<TResult> __RequestFireAndForget<TMessage, TResult>(string service, string method, TMessage message, Func<TMessage, ReadOnlySequence<byte>> intransform, Func<ReadOnlySequence<byte>, TResult> outtransform, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default) =>
			outtransform(await __RequestFireAndForget(service, method, intransform(message), metadata, tracing));

		protected async Task<ReadOnlySequence<byte>> __RequestFireAndForget(string service, string method, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default)
		{
			var receiver = new Receiver();
			await Client.RequestResponse(receiver, data, new RemoteProcedureCall.RemoteProcedureCallMetadata(service, method, metadata, tracing));
			return await receiver.Task.ConfigureAwait(false);
		}


		protected async Task<TResult> __RequestResponse<TMessage, TResult>(TMessage message, Func<TMessage, byte[]> intransform, Func<byte[], TResult> outtransform, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default) =>
			outtransform((await __RequestResponse(new ReadOnlySequence<byte>(intransform(message)), metadata, tracing, service: service, method: method)).ToArray());

		protected async Task<TResult> __RequestResponse<TMessage, TResult>(TMessage message, Func<TMessage, ReadOnlySequence<byte>> intransform, Func<ReadOnlySequence<byte>, TResult> outtransform, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default) =>
			outtransform(await __RequestResponse(intransform(message), metadata, tracing, service: service, method: method));

		protected async Task<ReadOnlySequence<byte>> __RequestResponse(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default)
		{
			var receiver = new Receiver();
			await Client.RequestResponse(receiver, data, new RemoteProcedureCall.RemoteProcedureCallMetadata(service, method, metadata, tracing));
			return await receiver.Task.ConfigureAwait(false);
		}

		//TODO Ask about semantics of this - should it execute the server call before subscription?

		protected async Task<ReadOnlySequence<byte>> __RequestStream<TResult>(string service, string method, ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default)
		{
			var receiver = new Receiver();
			await Client.RequestStream(receiver, data, new RemoteProcedureCall.RemoteProcedureCallMetadata(service, method, metadata, tracing), initial: 3);		//TODO Policy!!
			return await receiver.Task.ConfigureAwait(false);
		}


		//protected void RequestStream(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestStream(null, data, metadata); }   //TODO Initial? Or get from policy?
		//protected void RequestChannel(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestChannel(null, data, metadata); } //TODO Initial?


		//private class Receiver : Receiver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>, IRSocketStream { }

		private class Receiver : TaskCompletionSource<ReadOnlySequence<byte>>, IRSocketStream
		{
			public void OnCompleted() { }
			public void OnError(Exception error) => base.SetException(error);
			public void OnNext((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value) => base.SetResult(value.data);
		}


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


		private Lazy<List<System.Reflection.MethodInfo>> Methods = new Lazy<List<System.Reflection.MethodInfo>>(() => GetMethods());

		static public List<System.Reflection.MethodInfo> GetMethods() => (
			from method in typeof(T).GetMethods()
			where method.IsPublic && method.DeclaringType == typeof(T)
			select method).ToList();
	}
}
