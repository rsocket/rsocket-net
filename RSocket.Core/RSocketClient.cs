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

		public RSocketClient(IRSocketTransport transport, RSocketOptions options = default) : base(transport, options) { }

		public Task ConnectAsync(RSocketOptions options = default, byte[] data = default, byte[] metadata = default) => ConnectAsync(options ?? RSocketOptions.Default, data: data == default ? default : new ReadOnlySequence<byte>(data), metadata: metadata == default ? default : new ReadOnlySequence<byte>(metadata));

		public async Task ConnectAsync(RSocketOptions options, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			await Transport.StartAsync();
			Handler = Connect(CancellationToken.None);
			await Setup(options.KeepAlive, options.Lifetime, options.MetadataMimeType, options.DataMimeType, data: data, metadata: metadata);
		}

		/// <summary>A simplfied RSocket Client that operates only on UTF-8 strings.</summary>
		public class ForStrings
		{
			private readonly RSocketClient Client;
			public ForStrings(RSocketClient client) { Client = client; }
			public Task<string> RequestResponse(string data, string metadata = default) => Client.RequestResponse(value => Encoding.UTF8.GetString(value.Data.ToArray()), new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
			public IAsyncEnumerable<string> RequestStream(string data, string metadata = default)
			{
				return Client.RequestStream(value =>
				{
					return Encoding.UTF8.GetString(value.Data.ToArray());
				}, new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)), metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
			}

			public IAsyncEnumerable<string> RequestChannel(IAsyncEnumerable<string> inputs, string data = default, string metadata = default) =>
				Client.RequestChannel(inputs, input => new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(input)), result => Encoding.UTF8.GetString(result.Data.ToArray()),
					data == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(data)),
					metadata == default ? default : new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(metadata)));
		}
	}
}
