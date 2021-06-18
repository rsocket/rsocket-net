using System;
using System.Net;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Security;
using Pipelines.Sockets.Unofficial;

namespace RSocket.Transports
{
	//TODO Readd transport logging - worth it during debugging.
	public class SocketTransport : IRSocketTransport
	{
		private readonly bool ssl;
		private IPEndPoint Endpoint;
		private Socket Socket;

		internal Task Running { get; private set; } = Task.CompletedTask;
		//private CancellationTokenSource Cancellation;
#pragma warning disable CS0649
		private volatile bool Aborted;      //TODO Implement cooperative cancellation (and remove warning suppression)
#pragma warning restore CS0649

		public Uri Url { get; private set; }
		private LoggerFactory Logger;

		IDuplexPipe Front, Back;
		private IDuplexPipe socketPipe;
		public PipeReader Input => Front.Input;
		public PipeWriter Output => Front.Output;

		public SocketTransport(string url, PipeOptions outputOptions = default, PipeOptions inputOptions = default, bool ssl = false) : this(new Uri(url), outputOptions, inputOptions, ssl) { }
		public SocketTransport(Uri url, PipeOptions outputOptions = default, PipeOptions inputOptions = default, bool ssl = false)
		{
			this.ssl = ssl;
			Url = url;
			if (string.Compare(url.Scheme, "TCP", true) != 0) { throw new ArgumentException("Only TCP connections are supported.", nameof(Url)); }
			if (url.Port == -1) { throw new ArgumentException("TCP Port must be specified.", nameof(Url)); }

			//Options = options ?? WebSocketsTransport.DefaultWebSocketOptions;
			Logger = new Microsoft.Extensions.Logging.LoggerFactory(new[] { new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider() });
			(Front, Back) = DuplexPipe.CreatePair(outputOptions, inputOptions);
		}

		public async Task StartAsync(CancellationToken cancel = default)
		{
			var dns = await Dns.GetHostEntryAsync(Url.Host);
			if (dns.AddressList.Length == 0) { throw new InvalidOperationException($"Unable to resolve address."); }
			Endpoint = new IPEndPoint(dns.AddressList[0], Url.Port);

			Socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			Socket.Connect(dns.AddressList, Url.Port);  //TODO Would like this to be async... Why so serious???

			if (ssl)
			{
				var sslStream = new SslStream(new NetworkStream(Socket), false);
				await sslStream.AuthenticateAsClientAsync(Url.Host);

				socketPipe = StreamConnection.GetDuplex(sslStream);
			}
			else
			{
				socketPipe = SocketConnection.Create(Socket);
			}

			Running = ProcessSocketAsync();
		}

		public Task StopAsync() => Task.CompletedTask;		//TODO More graceful shutdown

		private async Task ProcessSocketAsync()
		{
			// Begin sending and receiving. Receiving must be started first because ExecuteAsync enables SendAsync.
			var receiving = StartReceiving();
			var sending = StartSending();

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


		private async Task StartReceiving()
		{
			var token = default(CancellationToken);	//Cancellation?.Token ?? default;
			
			try
			{
				while (!token.IsCancellationRequested)
				{
					var read = await socketPipe.Input.ReadAsync(token);

					if (read.IsCanceled) break;
					if (read.Buffer.IsEmpty && read.IsCompleted) break;

					foreach (var segment in read.Buffer)
					{
						await Back.Output.WriteAsync(segment, token);
					}

					//Log.MessageReceived(_logger, receive.MessageType, receive.Count, receive.EndOfMessage);
					socketPipe.Input.AdvanceTo(read.Buffer.End);

					var flushResult = await Back.Output.FlushAsync(token);
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
			finally { await Back.Output.CompleteAsync(); }
		}


		private async Task StartSending()
		{
			Exception error = null;

			try
			{
				while (true)
				{
					var result = await Back.Input.ReadAsync();
					var buffer = result.Buffer;

					try
					{
						if (result.IsCanceled) { break; }
						if (!buffer.IsEmpty)
						{
							try
							{
								foreach (var memory in buffer)
								{
									await socketPipe.Output.WriteAsync(memory);
								}
								
								//Log.SendPayload(_logger, buffer.Length);
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
						Back.Input.AdvanceTo(buffer.End);     //RSOCKET Framing
					}
				}
			}
			catch (Exception ex)
			{
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
