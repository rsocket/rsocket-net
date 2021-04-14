using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using IRSocketStream = System.IObserver<(System.Buffers.ReadOnlySequence<byte> metadata, System.Buffers.ReadOnlySequence<byte> data)>;

namespace RSocket
{
	public class RSocketClient : RSocket
	{
		Task Handler;
		RSocketOptions Options { get; set; }

		public RSocketClient(IRSocketTransport transport, RSocketOptions options = default) : base(transport, options)
		{
			this.Options = options ?? RSocketOptions.Default;
		}

		public Task ConnectAsync(RSocketOptions options = default, byte[] data = default, byte[] metadata = default, byte[] resumeToken = default) => ConnectAsync(options ?? this.Options, data: data == default ? default : new ReadOnlySequence<byte>(data), metadata: metadata == default ? default : new ReadOnlySequence<byte>(metadata), resumeToken: resumeToken);

		public async Task ConnectAsync(RSocketOptions options, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data, byte[] resumeToken = default)
		{
			await Transport.StartAsync();
			Handler = Connect(CancellationToken.None);
			await Setup(options.KeepAlive, options.Lifetime, options.MetadataMimeType, options.DataMimeType, resumeToken: resumeToken, data: data, metadata: metadata);
			this.StartKeepAlive(options.KeepAlive, options.Lifetime);
		}

		public Task Setup(TimeSpan keepalive, TimeSpan lifetime, string metadataMimeType = null, string dataMimeType = null, byte[] resumeToken = default, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default)
		{
			return this.SendSetup(keepalive, lifetime, metadataMimeType: metadataMimeType, dataMimeType: dataMimeType, resumeToken: resumeToken, data: data, metadata: metadata);
		}

		/// <summary>A simplfied RSocket Client that operates only on UTF-8 strings.</summary>
		public class ForStrings
		{
			private readonly RSocketClient Client;
			public ForStrings(RSocketClient client) { Client = client; }
			public Task<string> RequestResponse(string data, string metadata = default)
			{
				return Client.RequestResponse(data, metadata);
			}
			public IAsyncEnumerable<string> RequestStream(string data, string metadata = default)
			{
				return Client.RequestStream(data, metadata);
			}

			public IAsyncEnumerable<string> RequestChannel(IAsyncEnumerable<string> inputs, string data = default, string metadata = default)
			{
				return Client.RequestChannel(inputs, data, metadata);
			}
		}
	}
}
