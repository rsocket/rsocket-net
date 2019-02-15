using System;
using System.Buffers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace RSocket.Reactive
{
	//TODO This goes to its own assembly for dependency management.

	public class RSocketClientReactive : RSocketClient
	{
		public RSocketClientReactive(IRSocketTransport transport, RSocketOptions options = default) : base(transport, options) { }

		public ResultOf<TData, TMetadata> Of<TData, TMetadata>() => new ResultOf<TData, TMetadata>(this);

		public IObservable<(TData Data, TMetadata Metadata)> RequestChannel<TData, TMetadata, TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default, int initial = RSocketClient.INITIALDEFAULT) =>
			Observable.Create<(TData, TMetadata)>(observer => { var stream = new ReactiveStream<TData, TMetadata>(observer, ResponseDataDeserializer, ResponseMetadataDeserializer); base.RequestChannel<TRequestData, TRequestMetadata>(stream, data, metadata: metadata, initial: initial); return stream.Task; });

		public IObservable<(TData Data, TMetadata Metadata)> RequestStream<TData, TMetadata, TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default, int initial = RSocketClient.INITIALDEFAULT) =>
			Observable.Create<(TData, TMetadata)>(observer => { var stream = new ReactiveStream<TData, TMetadata>(observer, ResponseDataDeserializer, ResponseMetadataDeserializer); base.RequestStream<TRequestData, TRequestMetadata>(stream, data, metadata: metadata, initial: initial); return stream.Task; });

		public IObservable<(TData Data, TMetadata Metadata)> RequestResponse<TData, TMetadata, TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default) =>
			Observable.Create<(TData, TMetadata)>(observer => { var stream = new ReactiveStream<TData, TMetadata>(observer, ResponseDataDeserializer, ResponseMetadataDeserializer); base.RequestResponse<TRequestData, TRequestMetadata>(stream, data, metadata: metadata); return stream.Task; });

		public IObservable<(TData Data, TMetadata Metadata)> RequestFireAndForget<TData, TMetadata, TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default) =>
			Observable.Create<(TData, TMetadata)>(observer => { var stream = new ReactiveStream<TData, TMetadata>(observer, ResponseDataDeserializer, ResponseMetadataDeserializer); base.RequestFireAndForget<TRequestData, TRequestMetadata>(stream, data, metadata: metadata); return stream.Task; });


		private sealed class ReactiveStream<TData, TMetadata> : IRSocketStream
		{
			readonly IObserver<(TData, TMetadata)> Observer;
			readonly TaskCompletionSource<object> Completion = new TaskCompletionSource<object>();
			public Task Task => Completion.Task;
			IRSocketDeserializer ForData;
			IRSocketDeserializer ForMetadata;

			public ReactiveStream(IObserver<(TData, TMetadata)> observer, IRSocketDeserializer fordata, IRSocketDeserializer formetadata)
			{
				Observer = observer;
				ForData = fordata;
				ForMetadata = formetadata;
			}

			void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnNext((ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data) value) =>
				Observer.OnNext((ForData.Deserialize<TData>(value.data), ForMetadata.Deserialize<TMetadata>(value.metadata)));
			void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnCompleted() { Observer.OnCompleted(); Completion.SetResult(null); }
			void IObserver<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)>.OnError(Exception error) => throw new NotImplementedException();

		}

		public struct ResultOf<TData, TMetadata>
		{
			internal RSocketClientReactive Client;
			public ResultOf(RSocketClientReactive client) => Client = client;

			public IObservable<(TData Data, TMetadata Metadata)> RequestChannel<TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default, int initial = RSocketClient.INITIALDEFAULT) =>
				Client.RequestChannel<TData, TMetadata, TRequestData, TRequestMetadata>(data, metadata, initial: initial);
			public IObservable<(TData Data, TMetadata Metadata)> RequestChannel<TRequestData>(TRequestData data, int initial = RSocketClient.INITIALDEFAULT) =>
				Client.RequestChannel<TData, TMetadata, TRequestData, object>(data, default, initial: initial);

			//public IObservable<(TData Data, TMetadata Metadata)> RequestChannel<TRequestData>(IObservable<TRequestData> data, int initial = RSocketClient.INITIALDEFAULT)
			//{
			//	ChannelSubscription = data.Subscribe(() => 
			//	Client.RequestChannel<TData, TMetadata, TRequestData, object>(data, default, initial: initial);

			//}
	
			public IObservable<(TData Data, TMetadata Metadata)> RequestStream<TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default, int initial = RSocketClient.INITIALDEFAULT) =>
				Client.RequestStream<TData, TMetadata, TRequestData, TRequestMetadata>(data, metadata, initial: initial);
			public IObservable<(TData Data, TMetadata Metadata)> RequestStream<TRequestData>(TRequestData data, int initial = RSocketClient.INITIALDEFAULT) =>
				Client.RequestStream<TData, TMetadata, TRequestData, object>(data, default, initial: initial);

			public IObservable<(TData Data, TMetadata Metadata)> RequestResponse<TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default) =>
				Client.RequestResponse<TData, TMetadata, TRequestData, TRequestMetadata>(data, metadata);
			public IObservable<(TData Data, TMetadata Metadata)> RequestResponse<TRequestData>(TRequestData data) =>
				Client.RequestResponse<TData, TMetadata, TRequestData, object>(data, default);


			public IObservable<(TData Data, TMetadata Metadata)> RequestFireAndForget<TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default) =>
				Client.RequestFireAndForget<TData, TMetadata, TRequestData, TRequestMetadata>(data, metadata);
			public IObservable<(TData Data, TMetadata Metadata)> RequestFireAndForget<TRequestData>(TRequestData data) =>
				Client.RequestFireAndForget<TData, TMetadata, TRequestData, object>(data, default);
		}
	}


	//public class RSocketClientReactive<TSerializer> : RSocketClient where TSerializer : IRSocketSerializer, IRSocketDeserializer, new()
	//{
	//	public RSocketClientReactive(IRSocketTransport transport, RSocketClientOptions options = default) : base(transport, options) { }

	//	//public IObservable<T> RequestStream<T>(byte[] data, int initial = -1) => ReactiveStream<T>.Create(stream => base.RequestStream(stream, data, initial));
	//	//public IObservable<T> RequestStream<T>(byte[] data, string metadata = default, int initial = RSocketClient.INITIALDEFAULT) => Observable.Create<T>(observer => { var stream = new ReactiveStream<T>(observer); base.RequestStream(stream, data, metadata: metadata, initial: initial); return stream.Task; });

	//	public class ReactiveStream<T> : IRSocketStream
	//	{
	//		readonly IObserver<T> Observer;
	//		readonly TaskCompletionSource<object> Completion = new TaskCompletionSource<object>();
	//		public Task Task => Completion.Task;

	//		public ReactiveStream(IObserver<T> observer) { Observer = observer; }

	//		void IRSocketStream.Next(ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> data) => Observer.OnNext(Serializer.Deserialize<T>(data));
	//		void IRSocketStream.Complete() { Observer.OnCompleted(); Completion.SetResult(null); }

	//		static readonly TSerializer Serializer = new TSerializer();
	//		//static public IObservable<T> Create(Action<IRSocketStream> action) => Observable.Create<T>(observer => { var stream = new ReactiveStream<T>(observer); action(stream); return stream.Task; });
	//	}
	//}


	//public class RSocketClientReactive<TDataSerializer, TMetadataSerializer> : RSocketClient where TDataSerializer : IRSocketSerializer, IRSocketDeserializer, new() where TMetadataSerializer : IRSocketSerializer, IRSocketDeserializer, new()
	//{
	//	public RSocketClientReactive(IRSocketTransport transport, RSocketClientOptions options = default) : base(transport, options) { }

	//	public IObservable<(TData, TMetadata)> RequestStream<TData, TMetadata, TRequestData, TRequestMetadata>(TRequestData data, TRequestMetadata metadata = default, int initial = RSocketClient.INITIALDEFAULT) =>
	//		Observable.Create<(TData, TMetadata)>(observer => { var stream = new ReactiveStream<TData, TMetadata>(observer); base.RequestStream(stream, data, metadata: metadata, initial: initial); return stream.Task; });


	//	private sealed class ReactiveStream<TData, TMetadata> : IRSocketStream
	//	{
	//		readonly IObserver<(TData, TMetadata)> Observer;
	//		readonly TaskCompletionSource<object> Completion = new TaskCompletionSource<object>();
	//		public Task Task => Completion.Task;

	//		public ReactiveStream(IObserver<(TData, TMetadata)> observer) { Observer = observer; }

	//		void IRSocketStream.Next(ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> data) => Observer.OnNext((DataSerializer.Deserialize<TData>(data), MetadataSerializer.Deserialize<TMetadata>(metadata)));
	//		void IRSocketStream.Complete() { Observer.OnCompleted(); Completion.SetResult(null); }

	//		static readonly TDataSerializer DataSerializer = new TDataSerializer();
	//		static readonly TMetadataSerializer MetadataSerializer = new TMetadataSerializer();
	//	}
	//}
}
