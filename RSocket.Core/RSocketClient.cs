using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	//public interface IRSocketReceive
	//{
	//	void Error(RSocketProtocol.ErrorCodes errorcode, string errordata);
	//}

	public interface IRSocketSerializer
	{
		ReadOnlySpan<byte> Serialize<T>(in T item);
		T Deserialize<T>(in ReadOnlySpan<byte> data);		//FUTURE C#8.0 static interface member
	}

	public interface IRSocketTransform
	{
		void Next(ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> data);
		void Complete();
	}

	//public class RSocketClientReactive<TSerializer> : RSocketClient where TSerializer : IRSocketSerializer, new()
	//{
	//	public RSocketClientReactive(IRSocketTransport transport, RSocketClientOptions options = default) : base(transport, options) { }

	//	public async ValueTask<IObservable<T>> RequestStream<T>(byte[] data, int initial = -1) => await base.RequestStream(new ReactiveTransform<T>(), data, initial);

	//	public class ReactiveTransform<T> : IRSocketTransform, IObservable<T>
	//	{
	//		ReplaySubject<T> Subject = new ReplaySubject<T>();
	//		static readonly IRSocketSerializer Serializer = new TSerializer();

	//		//TODO Review with the assumption of other methods here for the various accumulation modes.
	//		void IRSocketTransform.Next(ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> data) => Subject.OnNext(Serializer.Deserialize<T>(data));
	//		void IRSocketTransform.Complete() => Subject.OnCompleted();

	//		public IDisposable Subscribe(IObserver<T> observer) => ((IObservable<T>)Subject).Subscribe(observer);
	//	}
	//}

	public class RSocketClient : IRSocketProtocol
	{
		IRSocketTransport Transport;
		RSocketClientOptions Options;
		private int StreamId = 1;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
		private int NewStreamId() => Interlocked.Add(ref StreamId, 2);

		private ConcurrentDictionary<int, IRSocketTransform> Dispatcher = new ConcurrentDictionary<int, IRSocketTransform>();
		private int BindDispatch(IRSocketTransform transform) { var id = NewStreamId(); Dispatcher[id] = transform; return id; }
		//TODO Stream Destruction - i.e. removal from the dispatcher.


		public RSocketClient(IRSocketTransport transport, RSocketClientOptions options = default)
		{
			Transport = transport;
			Options = options ?? RSocketClientOptions.Default;
		}

		public async Task ConnectAsync()
		{
			await Transport.ConnectAsync();
			var server = RSocketProtocol.Server(this, Transport.Input, CancellationToken.None);
			//TODO Move defaults to policy object
			new RSocketProtocol.Setup(keepalive: TimeSpan.FromSeconds(60), lifetime: TimeSpan.FromSeconds(180), metadataMimeType: "binary", dataMimeType: "binary").Write(Transport.Output);
			await Transport.Output.FlushAsync();
		}

		public async ValueTask Test(string text)
		{
			await Transport.ConnectAsync();
			var server = RSocketProtocol.Server(this, Transport.Input, CancellationToken.None);
			new RSocketProtocol.Test(text).Write(Transport.Output);
			await Transport.Output.FlushAsync();
		}

		void IRSocketProtocol.Payload(in RSocketProtocol.Payload value)
		{
			Console.WriteLine($"{value.Header.Stream:0000}===>{Encoding.UTF8.GetString(value.Data.ToArray())}");
			if (Dispatcher.TryGetValue(value.Header.Stream, out var transform))
			{
				if (value.Next) { transform.Next(value.Metadata, value.Data); }
				if (value.Complete) { transform.Complete(); }
			}
			else
			{
				//TODO Log missing stream here.
			}
		}

		//TODO Add back overloads for metadata
		public async ValueTask<TTransform> RequestStream<TTransform>(TTransform transform, byte[] data, int initial = -1) where TTransform : IRSocketTransform
		{
			if (initial < 0) { initial = Options.InitialRequestSize; }
			var id = BindDispatch(transform);
			new RSocketProtocol.RequestStream(id, data, initialRequest: initial).Write(Transport.Output);
			await Transport.Output.FlushAsync();
			return transform;
		}

		public async ValueTask<TTransform> RequestChannel<TTransform>(TTransform transform, byte[] data, int initial = -1) where TTransform : IRSocketTransform
		{
			if (initial < 0) { initial = Options.InitialRequestSize; }
			var id = BindDispatch(transform);
			new RSocketProtocol.RequestChannel(id, data, initialRequest: initial).Write(Transport.Output);
			await Transport.Output.FlushAsync();
			return transform;
		}

		//TODO Errors
	}
}
