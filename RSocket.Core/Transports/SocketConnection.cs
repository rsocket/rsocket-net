using Microsoft.Extensions.Logging;
using RSocket.Transports;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket.Transports
{
	public class SocketConnection : IRSocketTransport
	{
		Socket Socket;

		public string ConnectionId { get; } = Guid.NewGuid().ToString();

		public Task Running { get; private set; } = Task.CompletedTask;
		//private CancellationTokenSource Cancellation;
		private volatile bool Aborted;      //TODO Implement cooperative cancellation (and remove warning suppression)

		private LoggerFactory Logger;

		IDuplexPipe Front, Back;
		public PipeReader Input => Front.Input;
		public PipeWriter Output => Front.Output;

		public SocketConnection(Socket socket, PipeOptions outputoptions = default, PipeOptions inputoptions = default)
		{
			this.Socket = socket;

			Logger = new Microsoft.Extensions.Logging.LoggerFactory(new[] { new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider() });
			(Front, Back) = DuplexPipe.CreatePair(outputoptions, inputoptions);
		}

		public async Task StartAsync(CancellationToken cancel = default)
		{
			Running = ProcessSocketAsync(Socket);
		}

		public async Task StopAsync()
		{
			this.Socket.Close();
			this.Socket.Dispose();
			this.Front.Input.Complete();
			this.Front.Output.Complete();
			this.Back.Input.Complete();
			this.Back.Input.Complete();
		}

		private async Task ProcessSocketAsync(Socket socket)
		{
			// Begin sending and receiving. Receiving must be started first because ExecuteAsync enables SendAsync.
			var receiving = StartReceiving(socket);
			var sending = StartSending(socket);

			await Task.WhenAll(receiving, sending);
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
				if (!Aborted && !token.IsCancellationRequested)
				{
					Back.Output.Complete(ex); throw;
				}
			}
			finally
			{
				Back.Output.Complete();
			}
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
						Back.Input.AdvanceTo(consumed, buffer.End);     //RSOCKET Framing
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
