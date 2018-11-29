using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	public interface IRSocketSerializer
	{
		ReadOnlySpan<byte> Serialize<T>(in T item);
		T Deserialize<T>(in ReadOnlySpan<byte> data);		//FUTURE C#8.0 static interface member
	}

	public interface IRSocketStream
	{
		//TODO Need to really think about single/multiple here. OptionalFeaturesPattern?
		void Next(ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> data);
		void Complete();
	}

	public class RSocketClient : IRSocketProtocol
	{
		public const int INITIALDEFAULT = -1;

		IRSocketTransport Transport;
		RSocketClientOptions Options;
		private int StreamId = 1;       //SPEC: Stream IDs on the client MUST start at 1 and increment by 2 sequentially, such as 1, 3, 5, 7, etc
		private int NewStreamId() => Interlocked.Add(ref StreamId, 2);

		private ConcurrentDictionary<int, IRSocketStream> Dispatcher = new ConcurrentDictionary<int, IRSocketStream>();
		private int StreamDispatch(IRSocketStream transform) { var id = NewStreamId(); Dispatcher[id] = transform; return id; }
		//TODO Stream Destruction - i.e. removal from the dispatcher.


		public RSocketClient(IRSocketTransport transport, RSocketClientOptions options = default)
		{
			Transport = transport;
			Options = options ?? RSocketClientOptions.Default;
		}

		public async Task ConnectAsync()
		{
			await Transport.ConnectAsync();
			var server = RSocketProtocol.Handler(this, Transport.Input, CancellationToken.None);
			//TODO Move defaults to policy object
			new RSocketProtocol.Setup(keepalive: TimeSpan.FromSeconds(60), lifetime: TimeSpan.FromSeconds(180), metadataMimeType: "binary", dataMimeType: "binary").Write(Transport.Output);
			await Transport.Output.FlushAsync();
		}

		public async ValueTask Test(string text)
		{
			await Transport.ConnectAsync();
			var server = RSocketProtocol.Handler(this, Transport.Input, CancellationToken.None);
			new RSocketProtocol.Test(text).Write(Transport.Output);
			await Transport.Output.FlushAsync();
		}

		void IRSocketProtocol.Payload(in RSocketProtocol.Payload value)
		{
			//Console.WriteLine($"{value.Header.Stream:0000}===>{Encoding.UTF8.GetString(value.Data.ToArray())}");
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
		
		#region Default Serializers for Strings
		public ValueTask<System.IO.Pipelines.FlushResult> RequestStream(IRSocketStream transform, Span<byte> data, string metadata = default, int initial = INITIALDEFAULT) =>
			RequestStream(transform, data, metadata: metadata == default ? default : Encoding.UTF8.GetBytes(metadata), initial: initial);
		#endregion


		public ValueTask<System.IO.Pipelines.FlushResult> RequestStream(IRSocketStream transform, Span<byte> data, Span<byte> metadata = default, int initial = INITIALDEFAULT)
		{
			if (initial < 0) { initial = Options.InitialRequestSize; }
			var id = StreamDispatch(transform);
			new RSocketProtocol.RequestStream(id, data, metadata, initialRequest: initial).Write(Transport.Output);
			return Transport.Output.FlushAsync();
		}

		public ValueTask<System.IO.Pipelines.FlushResult> RequestChannel(IRSocketStream transform, Span<byte> data, Span<byte> metadata = default, int initial = INITIALDEFAULT)
		{
			if (initial < 0) { initial = Options.InitialRequestSize; }
			var id = StreamDispatch(transform);
			new RSocketProtocol.RequestChannel(id, data, initialRequest: initial).Write(Transport.Output);
			return Transport.Output.FlushAsync();
		}

		//TODO Errors
	}
}
