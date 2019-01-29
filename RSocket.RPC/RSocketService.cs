using System;
using System.Buffers;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace RSocket.RPC
{
	public abstract partial class RSocketService
	{
		private readonly RSocketClient Client;

		public RSocketService(RSocketClient client) { Client = client; }

		protected Task __RequestFireAndForget<TMessage>(TMessage message, Func<TMessage, byte[]> intransform, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default) => __RequestFireAndForget(new ReadOnlySequence<byte>(intransform(message)), metadata, tracing, service: service, method: method);
		protected Task __RequestFireAndForget<TMessage>(TMessage message, Func<TMessage, ReadOnlySequence<byte>> intransform, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default) => __RequestFireAndForget(intransform(message), metadata, tracing, service: service, method: method);
		protected async Task __RequestFireAndForget(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default)
		{
			var receiver = new Receiver();
			await Client.RequestFireAndForget(receiver, data, new RemoteProcedureCallMetadata(service, method, metadata, tracing)).ConfigureAwait(false);
			receiver.TrySetResult(default);
			await receiver.Awaitable;
		}


		protected async Task<TResult> __RequestResponse<TMessage, TResult>(TMessage message, Func<TMessage, byte[]> intransform, Func<byte[], TResult> outtransform, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default) => outtransform((await __RequestResponse(new ReadOnlySequence<byte>(intransform(message)), metadata, tracing, service: service, method: method)).ToArray());
		protected async Task<TResult> __RequestResponse<TMessage, TResult>(TMessage message, Func<TMessage, ReadOnlySequence<byte>> intransform, Func<ReadOnlySequence<byte>, TResult> outtransform, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default) => outtransform(await __RequestResponse(intransform(message), metadata, tracing, service: service, method: method));
		protected async Task<ReadOnlySequence<byte>> __RequestResponse(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default)
		{
			var receiver = new Receiver();
			await Client.RequestResponse(receiver, data, new RemoteProcedureCallMetadata(service, method, metadata, tracing));
			return await receiver.Awaitable;
		}

		//TODO Ask about semantics of this - should it execute the server call before subscription?

		protected async Task<ReadOnlySequence<byte>> __RequestStream<TResult>(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default, ReadOnlySequence<byte> tracing = default, string service = default, [CallerMemberName]string method = default)
		{
			var receiver = new Receiver();
			await Client.RequestStream(receiver, data, new RemoteProcedureCallMetadata(service, method, metadata, tracing), initial: 3);		//TODO Policy!!
			return await receiver.Task.ConfigureAwait(false);
		}


		//protected void RequestStream(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestStream(null, data, metadata); }   //TODO Initial? Or get from policy?
		//protected void RequestChannel(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata = default) { Client.RequestChannel(null, data, metadata); } //TODO Initial?


		private class Receiver : TaskCompletionSource<ReadOnlySequence<byte>>, IRSocketStream
		{
			public void OnCompleted() { }
			public void OnError(Exception error) => base.SetException(error);
			public void OnNext((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value) => base.SetResult(value.data);
			public ConfiguredTaskAwaitable<ReadOnlySequence<byte>> Awaitable => base.Task.ConfigureAwait(false);
		}
	}
}
