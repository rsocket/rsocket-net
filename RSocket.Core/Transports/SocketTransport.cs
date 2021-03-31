using System;
using System.Net;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Buffers;

namespace RSocket.Transports
{
	//TODO Readd transport logging - worth it during debugging.
	public class SocketTransport : IRSocketTransport
	{
		private Socket Socket;

		internal Task Running { get; private set; } = Task.CompletedTask;
		//private CancellationTokenSource Cancellation;
		private volatile bool Aborted;      //TODO Implement cooperative cancellation (and remove warning suppression)

		IPAddress IP { get; set; }
		int Port { get; set; }

		private LoggerFactory Logger;

		IDuplexPipe Front, Back;
		public PipeReader Input => Front.Input;
		public PipeWriter Output => Front.Output;


		public static async Task<SocketTransport> Create(string url, PipeOptions outputoptions = default, PipeOptions inputoptions = default)
		{
			return await Create(new Uri(url), outputoptions, inputoptions);
		}
		public static async Task<SocketTransport> Create(Uri url, PipeOptions outputoptions = default, PipeOptions inputoptions = default)
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

			return new SocketTransport(ip, url.Port, outputoptions, inputoptions);
		}

		public SocketTransport(string ip, int port, PipeOptions outputoptions = default, PipeOptions inputoptions = default, WebSocketOptions options = default) : this(IPAddress.Parse(ip), port, outputoptions, inputoptions, options)
		{
		}
		public SocketTransport(IPAddress ip, int port, PipeOptions outputoptions = default, PipeOptions inputoptions = default, WebSocketOptions options = default)
		{
			this.IP = ip;
			this.Port = port;

			//Options = options ?? WebSocketsTransport.DefaultWebSocketOptions;
			Logger = new Microsoft.Extensions.Logging.LoggerFactory(new[] { new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider() });
			(Front, Back) = DuplexPipe.CreatePair(outputoptions, inputoptions);
		}

		public async Task StartAsync(CancellationToken cancel = default)
		{
			await this.StartAsync(this.IP, this.Port);
		}

		async Task StartAsync(IPAddress ip, int port, CancellationToken cancel = default)
		{
			this.Socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			this.Socket.Connect(ip, port);  //TODO Would like this to be async... Why so serious???

			this.Running = ProcessSocketAsync(Socket);
		}

		public async Task StopAsync()
		{
			this.Front.Input.Complete();
			this.Front.Output.Complete();
			this.Socket.Shutdown(SocketShutdown.Both);
		}

		private async Task ProcessSocketAsync(Socket socket)
		{
			// Begin sending and receiving. Receiving must be started first because ExecuteAsync enables SendAsync.
			var receiving = StartReceiving(socket);
			var sending = StartSending(socket);

			var trigger = await Task.WhenAny(receiving, sending);

			//if (trigger == receiving)
			//{
			//	Log.WaitingForSend(_logger);

			//	// We're waiting for the application to finish and there are 2 things it could be doing
			//	// 1. Waiting for application data
			//	// 2. Waiting for a websocket send to complete

			//	// Cancel the application so that ReadAsync yields
			//	_application.Input.CancelPendingRead();

			//	using (var delayCts = new CancellationTokenSource())
			//	{
			//		var resultTask = await Task.WhenAny(sending, Task.Delay(_options.CloseTimeout, delayCts.Token));

			//		if (resultTask != sending)
			//		{
			//			// We timed out so now we're in ungraceful shutdown mode
			//			Log.CloseTimedOut(_logger);

			//			// Abort the websocket if we're stuck in a pending send to the client
			//			_aborted = true;

			//			socket.Abort();
			//		}
			//		else
			//		{
			//			delayCts.Cancel();
			//		}
			//	}
			//}
			//else
			//{
			//	Log.WaitingForClose(_logger);

			//	// We're waiting on the websocket to close and there are 2 things it could be doing
			//	// 1. Waiting for websocket data
			//	// 2. Waiting on a flush to complete (backpressure being applied)

			//	using (var delayCts = new CancellationTokenSource())
			//	{
			//		var resultTask = await Task.WhenAny(receiving, Task.Delay(_options.CloseTimeout, delayCts.Token));

			//		if (resultTask != receiving)
			//		{
			//			// Abort the websocket if we're stuck in a pending receive from the client
			//			_aborted = true;

			//			socket.Abort();

			//			// Cancel any pending flush so that we can quit
			//			_application.Output.CancelPendingFlush();
			//		}
			//		else
			//		{
			//			delayCts.Cancel();
			//		}
			//	}
			//}
		}


		private async Task StartReceiving(Socket socket)
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
					var memory = Back.Output.GetMemory(out var memoryframe, haslength: true);    //RSOCKET Framing
					var isArray = MemoryMarshal.TryGetArray<byte>(memory, out var arraySegment); Debug.Assert(isArray);

					var received = await socket.ReceiveAsync(arraySegment, SocketFlags.None);   //TODO Cancellation?
#endif
					//Log.MessageReceived(_logger, receive.MessageType, receive.Count, receive.EndOfMessage);
					Back.Output.Advance(received);
					var flushResult = await Back.Output.FlushAsync();
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
				if (!Aborted && !token.IsCancellationRequested) { Back.Output.Complete(ex); throw; }
			}
			finally { Back.Output.Complete(); }
		}


		private async Task StartSending(Socket socket)
		{
			Exception error = null;

			try
			{
				while (true)
				{
					var result = await Back.Input.ReadAsync();
					var buffer = result.Buffer;
					var consumed = buffer.Start;        //RSOCKET Framing

					try
					{
						if (result.IsCanceled)
						{
							break;
						}

						if (!buffer.IsEmpty)
						{
							try
							{
								//Log.SendPayload(_logger, buffer.Length);
								consumed = await socket.SendAsync(buffer, buffer.Start, SocketFlags.None);     //RSOCKET Framing
							}
							catch (Exception ex)
							{
								Console.WriteLine($"error socket.SendAsync: {ex.Message}");
								if (!Aborted) { /*Log.ErrorWritingFrame(_logger, ex);*/ }
								break;
							}
						}
						else if (result.IsCompleted) { break; }
					}
					finally
					{
						Back.Input.AdvanceTo(consumed, buffer.End);     //RSOCKET Framing
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"error socket.SendAsync: {ex.Message}");
				error = ex;
			}
			finally
			{
				//// Send the close frame before calling into user code
				//if (WebSocketCanSend(socket))
				//{
				//	// We're done sending, send the close frame to the client if the websocket is still open
				//	await socket.CloseOutputAsync(error != null ? WebSocketCloseStatus.InternalServerError : WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
				//}
				Back.Input.Complete();
			}

		}
	}
}


namespace System.Net.Sockets
{
	internal static class SocketExtensions
	{
		public static ValueTask SendAsync(this Socket socket, ReadOnlySequence<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default)
		{
#if NETCOREAPP3_0
            if (buffer.IsSingleSegment)
            {
                return socket.SendAsync(buffer.First, webSocketMessageType, endOfMessage: true, cancellationToken);
            }
            else { return SendMultiSegmentAsync(socket, buffer, socketFlags, cancellationToken); }
#else
			if (buffer.IsSingleSegment)
			{
				var isArray = MemoryMarshal.TryGetArray(buffer.First, out var segment);
				Debug.Assert(isArray);
				return new ValueTask(socket.SendAsync(segment, socketFlags));       //TODO Cancellation?
			}
			else { return SendMultiSegmentAsync(socket, buffer, socketFlags, cancellationToken); }
#endif
		}

		static async ValueTask SendMultiSegmentAsync(Socket socket, ReadOnlySequence<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default)
		{
#if NETCOREAPP3_0
			var position = buffer.Start;
			buffer.TryGet(ref position, out var prevSegment);
			while (buffer.TryGet(ref position, out var segment))
			{
				await socket.SendAsync(prevSegment, socketFlags);
				prevSegment = segment;
			}
			await socket.SendAsync(prevSegment, socketFlags);
#else
			var position = buffer.Start;
			buffer.TryGet(ref position, out var prevSegment);
			while (buffer.TryGet(ref position, out var segment))
			{
				var isArray = MemoryMarshal.TryGetArray(prevSegment, out var arraySegment);
				Debug.Assert(isArray);
				await socket.SendAsync(arraySegment, socketFlags);
				prevSegment = segment;
			}
			var isArrayEnd = MemoryMarshal.TryGetArray(prevSegment, out var arraySegmentEnd);
			Debug.Assert(isArrayEnd);
			await socket.SendAsync(arraySegmentEnd, socketFlags);
#endif
		}
	}
}
