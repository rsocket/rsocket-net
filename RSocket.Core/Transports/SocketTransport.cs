using Microsoft.Extensions.Logging;
using RSocket.Transports;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket.Transports
{
	public class ServerSocketTransport : SocketTransport, IRSocketTransport
	{
		Socket _socket;
		public ServerSocketTransport(Socket socket, PipeOptions outputOptions = default, PipeOptions inputOptions = default) : base(outputOptions, inputOptions)
		{
			this._socket = socket;
		}

		protected override async Task<Socket> CreateSocket()
		{
			await Task.CompletedTask;
			return this._socket;
		}
	}

	public class ClientSocketTransport : SocketTransport, IRSocketTransport
	{
		IPAddress IP { get; set; }
		int Port { get; set; }

		public static async Task<ClientSocketTransport> Create(string url, PipeOptions outputoptions = default, PipeOptions inputoptions = default)
		{
			return await Create(new Uri(url), outputoptions, inputoptions);
		}
		public static async Task<ClientSocketTransport> Create(Uri url, PipeOptions outputoptions = default, PipeOptions inputoptions = default)
		{
			if (string.Compare(url.Scheme, "TCP", true) != 0)
			{
				throw new ArgumentException("Only TCP connections are supported.", nameof(url));
			}
			if (url.Port == -1)
			{
				throw new ArgumentException("TCP Port must be specified.", nameof(url));
			}

			var dns = await Dns.GetHostEntryAsync(url.Host);
			if (dns.AddressList.Length == 0) { throw new InvalidOperationException($"Unable to resolve address."); }

			IPAddress ip = dns.AddressList[0];

			return new ClientSocketTransport(ip, url.Port, outputoptions, inputoptions);
		}

		public ClientSocketTransport(string ip, int port, PipeOptions outputOptions = default, PipeOptions inputOptions = default, WebSocketOptions options = default) : this(IPAddress.Parse(ip), port, outputOptions, inputOptions, options)
		{
		}
		public ClientSocketTransport(IPAddress ip, int port, PipeOptions outputOptions = default, PipeOptions inputOptions = default, WebSocketOptions options = default) : base(outputOptions, inputOptions)
		{
			this.IP = ip;
			this.Port = port;
		}

		protected override async Task<Socket> CreateSocket()
		{
			await Task.CompletedTask;
			Socket socket = new Socket(this.IP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(this.IP, this.Port);  //TODO Would like this to be async... Why so serious???
			return socket;
		}
	}

	public abstract class SocketTransport : IRSocketTransport
	{
		Socket _socket;

		public Task Running { get; private set; } = Task.CompletedTask;
		//private CancellationTokenSource Cancellation;
		volatile bool Aborted;      //TODO Implement cooperative cancellation (and remove warning suppression)

		LoggerFactory Logger;

		protected IDuplexPipe Front { get; private set; }
		protected IDuplexPipe Back { get; private set; }

		public PipeReader Input => Front.Input;
		public PipeWriter Output => Front.Output;

		protected SocketTransport(PipeOptions outputOptions = default, PipeOptions inputOptions = default)
		{
			Logger = new LoggerFactory(new[] { new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider() });
			(Front, Back) = DuplexPipe.CreatePair(outputOptions, inputOptions);
		}

		public virtual async Task StartAsync(CancellationToken cancel = default)
		{
			this._socket = await this.CreateSocket();
			Running = ProcessSocketAsync(this._socket);
		}

		public virtual async Task StopAsync()
		{
			await Task.CompletedTask;
			this.Front.Input.Complete();
			this.Front.Output.Complete();
			this._socket?.Shutdown(SocketShutdown.Both);
		}

		protected abstract Task<Socket> CreateSocket();

		async Task ProcessSocketAsync(Socket socket)
		{
			// Begin sending and receiving. Receiving must be started first because ExecuteAsync enables SendAsync.
			var receiving = this.StartReceiving(socket);
			var sending = this.StartSending(socket);

			await Task.WhenAll(receiving, sending);
		}


		async Task StartReceiving(Socket socket)
		{
			var token = default(CancellationToken); //Cancellation?.Token ?? default;

			try
			{
				while (!token.IsCancellationRequested)
				{
#if NETCOREAPP3_0
                    // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                    var received = await socket.ReceiveAsync(Memory<byte>.Empty, token);
					if(received == 0) { continue; }
					var memory = Back.Output.GetMemory(out var memoryframe, haslength: true);    //RSOCKET Framing
                    var received = await socket.ReceiveAsync(memory, token);
#else
					var memory = this.Back.Output.GetMemory(out var memoryframe, haslength: true);    //RSOCKET Framing
					var isArray = MemoryMarshal.TryGetArray<byte>(memory, out var arraySegment); Debug.Assert(isArray);

					var received = await socket.ReceiveAsync(arraySegment, SocketFlags.None);   //TODO Cancellation?
#endif
					//Log.MessageReceived(_logger, receive.MessageType, receive.Count, receive.EndOfMessage);
					Back.Output.Advance(received);
					var flushResult = await this.Back.Output.FlushAsync();
					if (flushResult.IsCanceled || flushResult.IsCompleted) { break; }
				}
			}
			//catch (SocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
			//{
			//	// Client has closed the WebSocket connection without completing the close handshake
			//	Log.ClosedPrematurely(_logger, ex);
			//}
			catch (OperationCanceledException)
			{
				// Ignore aborts, don't treat them like transport errors
			}
			catch (Exception ex)
			{
				if (!Aborted && !token.IsCancellationRequested)
				{
					this.Back.Output.Complete(ex); throw;
				}
			}
			finally
			{
				this.Back.Output.Complete();
			}
		}


		async Task StartSending(Socket socket)
		{
			Exception error = null;

			try
			{
				while (true)
				{
					var result = await this.Back.Input.ReadAsync();
					var buffer = result.Buffer;
					var consumed = buffer.Start;        //RSOCKET Framing

					try
					{
						if (result.IsCanceled) { break; }
						if (!buffer.IsEmpty)
						{
							try
							{
								//Log.SendPayload(_logger, buffer.Length);
								consumed = await socket.SendAsync(buffer, buffer.Start, SocketFlags.None);     //RSOCKET Framing
							}
							catch (Exception)
							{
								if (!Aborted) { /*Log.ErrorWritingFrame(_logger, ex);*/ }
								break;
							}
						}
						else if (result.IsCompleted) { break; }
					}
					finally
					{
						this.Back.Input.AdvanceTo(consumed, buffer.End);     //RSOCKET Framing
					}
				}
			}
			catch (Exception ex)
			{
				error = ex;
			}
			finally
			{
				Back.Input.Complete();
			}

		}
	}
}
