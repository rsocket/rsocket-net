using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Buffers;

namespace RSocket.Transports
{
	public class WebSocketTransport : IRSocketTransport
	{
		private readonly WebSocketOptions Options;
		private readonly WebSocketsTransport Transport;
		public Uri Url { get; private set; }
		private LoggerFactory Logger;
		internal Task Running { get; private set; } = Task.CompletedTask;

		IDuplexPipe Front, Back;
		public PipeReader Input => Front.Input;
		public PipeWriter Output => Front.Output;

		public WebSocketTransport(string url, PipeOptions outputoptions = default, PipeOptions inputoptions = default) : this(new Uri(url), outputoptions, inputoptions) { }
		public WebSocketTransport(Uri url, PipeOptions outputoptions = default, PipeOptions inputoptions = default, WebSocketOptions options = default)
		{
			Url = url;
			Options = options ?? WebSocketsTransport.DefaultWebSocketOptions;
			Logger = new Microsoft.Extensions.Logging.LoggerFactory(new[] { new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider() });
			(Front, Back) = DuplexPipe.CreatePair(outputoptions, inputoptions);
			Transport = new WebSocketsTransport(Options, Back, WebSocketsTransport.HttpConnectionContext.Default, Logger);
		}

		public Task ConnectAsync(CancellationToken cancel = default)
		{
			Running = Transport.ProcessRequestAsync(new WebSocketsTransport.HttpContext(this), cancel);
			return Task.CompletedTask;
		}

		//This class is based heavily on the SignalR WebSocketsTransport class from the AspNetCore project. It was merged as release/2.2 as of this snapshot.
		//When designing Pipelines, for some reason, standard adapters for Sockets, WebSockets, etc were not published.
		//The SignalR team seems to be the driver for Pipelines, so it's expected that this code is the best implementation available.
		//Future updaters should review their progress here and switch to a standard WebSockets => Pipelines adapter when possible.

		//The verbatim copyright messages are below even though the code has been altered. The license is below as well.

		//Adapters for keeping the SignalR source as close to verbatim as possible. The SignalR code is a little junky here because it takes deeper dependencies than needed - most of this should be extracted in the constructor, but it's saved as a heavy reference only to be consumed once.
		public interface IHttpTransport { }
		partial class WebSocketsTransport
		{
			static public readonly WebSocketOptions DefaultWebSocketOptions = new WebSocketOptions();

			public enum TransferFormat { Binary = 1, Text = 2, }
			public class HttpConnectionContext
			{
				static public readonly HttpConnectionContext Default = new HttpConnectionContext();
				public readonly CancellationTokenSource Cancellation = default;
				public readonly TransferFormat ActiveFormat = TransferFormat.Binary;
			}

			public class HttpContext              //https://github.com/aspnet/AspNetCore/blob/master/src/Http/Http.Abstractions/src/HttpContext.cs
			{
				public readonly CancellationTokenSource Cancellation = default;
				public WebSocketManager WebSockets { get; }

				public HttpContext(WebSocketTransport transport, CancellationToken cancel = default) { WebSockets = new WebSocketManager(transport, cancel); }

				public class WebSocketManager     //https://github.com/aspnet/AspNetCore/blob/master/src/Http/Http/src/Internal/DefaultWebSocketManager.cs
				{
					public WebSocketTransport Transport;
					public CancellationToken Cancel;
					public bool IsWebSocketRequest => true;
					public IList<string> WebSocketRequestedProtocols => null;

					public WebSocketManager(WebSocketTransport transport, CancellationToken cancel = default) { Transport = transport; Cancel = cancel; }

					public async Task<WebSocket> AcceptWebSocketAsync(string subprotocol)     //https://github.com/aspnet/AspNetCore/blob/master/src/Middleware/WebSockets/src/WebSocketMiddleware.cs
					{
						//So in the SignalR code, this is where the WebSocketOptions are actually applied. So junky, so overabstracted. This is why the constructors are inverted so short out all of this madness.
						var socket = new ClientWebSocket();
						await socket.ConnectAsync(Transport.Url, Cancel);
						return socket;
					}
				}
			}
		}



		#region https://github.com/aspnet/AspNetCore/blob/master/src/SignalR/common/Http.Connections/src/Internal/Transports/WebSocketsTransport.cs

		// Copyright (c) .NET Foundation. All rights reserved.
		// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

		//using System;
		//using System.Diagnostics;
		//using System.IO.Pipelines;
		//using System.Net.WebSockets;
		//using System.Runtime.InteropServices;
		//using System.Threading;
		//using System.Threading.Tasks;
		//using Microsoft.AspNetCore.Http;
		//using Microsoft.AspNetCore.Connections;
		//using Microsoft.Extensions.Logging;

		public partial class WebSocketsTransport : IHttpTransport
		{
			private readonly WebSocketOptions _options;
			private readonly ILogger _logger;
			private readonly IDuplexPipe _application;
			private readonly HttpConnectionContext _connection;
			private volatile bool _aborted;

			public WebSocketsTransport(WebSocketOptions options, IDuplexPipe application, HttpConnectionContext connection, ILoggerFactory loggerFactory)
			{
				if (options == null)
				{
					throw new ArgumentNullException(nameof(options));
				}

				if (application == null)
				{
					throw new ArgumentNullException(nameof(application));
				}

				if (loggerFactory == null)
				{
					throw new ArgumentNullException(nameof(loggerFactory));
				}

				_options = options;
				_application = application;
				_connection = connection;
				_logger = loggerFactory.CreateLogger<WebSocketsTransport>();
			}

			public async Task ProcessRequestAsync(HttpContext context, CancellationToken token)
			{
				Debug.Assert(context.WebSockets.IsWebSocketRequest, "Not a websocket request");

				var subProtocol = _options.SubProtocolSelector?.Invoke(context.WebSockets.WebSocketRequestedProtocols);

				using (var ws = await context.WebSockets.AcceptWebSocketAsync(subProtocol))
				{
					Log.SocketOpened(_logger, subProtocol);

					try
					{
						await ProcessSocketAsync(ws);
					}
					finally
					{
						Log.SocketClosed(_logger);
					}
				}
			}

			public async Task ProcessSocketAsync(WebSocket socket)
			{
				// Begin sending and receiving. Receiving must be started first because ExecuteAsync enables SendAsync.
				var receiving = StartReceiving(socket);
				var sending = StartSending(socket);

				// Wait for send or receive to complete
				var trigger = await Task.WhenAny(receiving, sending);

				if (trigger == receiving)
				{
					Log.WaitingForSend(_logger);

					// We're waiting for the application to finish and there are 2 things it could be doing
					// 1. Waiting for application data
					// 2. Waiting for a websocket send to complete

					// Cancel the application so that ReadAsync yields
					_application.Input.CancelPendingRead();

					using (var delayCts = new CancellationTokenSource())
					{
						var resultTask = await Task.WhenAny(sending, Task.Delay(_options.CloseTimeout, delayCts.Token));

						if (resultTask != sending)
						{
							// We timed out so now we're in ungraceful shutdown mode
							Log.CloseTimedOut(_logger);

							// Abort the websocket if we're stuck in a pending send to the client
							_aborted = true;

							socket.Abort();
						}
						else
						{
							delayCts.Cancel();
						}
					}
				}
				else
				{
					Log.WaitingForClose(_logger);

					// We're waiting on the websocket to close and there are 2 things it could be doing
					// 1. Waiting for websocket data
					// 2. Waiting on a flush to complete (backpressure being applied)

					using (var delayCts = new CancellationTokenSource())
					{
						var resultTask = await Task.WhenAny(receiving, Task.Delay(_options.CloseTimeout, delayCts.Token));

						if (resultTask != receiving)
						{
							// Abort the websocket if we're stuck in a pending receive from the client
							_aborted = true;

							socket.Abort();

							// Cancel any pending flush so that we can quit
							_application.Output.CancelPendingFlush();
						}
						else
						{
							delayCts.Cancel();
						}
					}
				}
			}

			private async Task StartReceiving(WebSocket socket)
			{
				var token = _connection.Cancellation?.Token ?? default;

				try
				{
					while (!token.IsCancellationRequested)
					{
#if NETCOREAPP3_0
                    // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                    var result = await socket.ReceiveAsync(Memory<byte>.Empty, token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
#endif
						var memory = _application.Output.GetMemory(out var memoryframe);        //RSOCKET Framing

#if NETCOREAPP3_0
                    var receiveResult = await socket.ReceiveAsync(memory, token);
#else
						var isArray = MemoryMarshal.TryGetArray<byte>(memory, out var arraySegment);
						Debug.Assert(isArray);

						// Exceptions are handled above where the send and receive tasks are being run.
						var receiveResult = await socket.ReceiveAsync(arraySegment, token);
#endif
						// Need to check again for netcoreapp3.0 because a close can happen between a 0-byte read and the actual read
						if (receiveResult.MessageType == WebSocketMessageType.Close)
						{
							return;
						}

						Log.MessageReceived(_logger, receiveResult.MessageType, receiveResult.Count, receiveResult.EndOfMessage);

						_application.Output.Advance(receiveResult.Count, receiveResult.EndOfMessage, memoryframe);		//RSOCKET Framing

						var flushResult = await _application.Output.FlushAsync();

						// We canceled in the middle of applying back pressure
						// or if the consumer is done
						if (flushResult.IsCanceled || flushResult.IsCompleted)
						{
							break;
						}
					}
				}
				catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
				{
					// Client has closed the WebSocket connection without completing the close handshake
					Log.ClosedPrematurely(_logger, ex);
				}
				catch (OperationCanceledException)
				{
					// Ignore aborts, don't treat them like transport errors
				}
				catch (Exception ex)
				{
					if (!_aborted && !token.IsCancellationRequested)
					{
						_application.Output.Complete(ex);

						// We re-throw here so we can communicate that there was an error when sending
						// the close frame
						throw;
					}
				}
				finally
				{
					// We're done writing
					_application.Output.Complete();
				}
			}

			private async Task StartSending(WebSocket socket)
			{
				Exception error = null;

				try
				{
					while (true)
					{
						var result = await _application.Input.ReadAsync();
						var buffer = result.Buffer;

						// Get a frame from the application

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
									Log.SendPayload(_logger, buffer.Length);

									var webSocketMessageType = (_connection.ActiveFormat == TransferFormat.Binary
										? WebSocketMessageType.Binary
										: WebSocketMessageType.Text);

									if (WebSocketCanSend(socket))
									{
										await socket.SendAsync(buffer, buffer.Start, webSocketMessageType);     //RSOCKET Framing
									}
									else
									{
										break;
									}
								}
								catch (Exception ex)
								{
									if (!_aborted)
									{
										Log.ErrorWritingFrame(_logger, ex);
									}
									break;
								}
							}
							else if (result.IsCompleted)
							{
								break;
							}
						}
						finally
						{
							_application.Input.AdvanceTo(buffer.End);
						}
					}
				}
				catch (Exception ex)
				{
					error = ex;
				}
				finally
				{
					// Send the close frame before calling into user code
					if (WebSocketCanSend(socket))
					{
						// We're done sending, send the close frame to the client if the websocket is still open
						await socket.CloseOutputAsync(error != null ? WebSocketCloseStatus.InternalServerError : WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
					}

					_application.Input.Complete();
				}

			}

			private static bool WebSocketCanSend(WebSocket ws)
			{
				return !(ws.State == WebSocketState.Aborted ||
					   ws.State == WebSocketState.Closed ||
					   ws.State == WebSocketState.CloseSent);
			}
		}
		#endregion

		#region https://github.com/aspnet/AspNetCore/blob/master/src/SignalR/common/Http.Connections/src/Internal/Transports/WebSocketsTransport.Log.cs
		// Copyright (c) .NET Foundation. All rights reserved.
		// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

		public partial class WebSocketsTransport
		{
			private static class Log
			{
				private static readonly Action<ILogger, string, Exception> _socketOpened =
					LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "SocketOpened"), "Socket opened using Sub-Protocol: '{SubProtocol}'.");

				private static readonly Action<ILogger, Exception> _socketClosed =
					LoggerMessage.Define(LogLevel.Debug, new EventId(2, "SocketClosed"), "Socket closed.");

				private static readonly Action<ILogger, WebSocketCloseStatus?, string, Exception> _clientClosed =
					LoggerMessage.Define<WebSocketCloseStatus?, string>(LogLevel.Debug, new EventId(3, "ClientClosed"), "Client closed connection with status code '{Status}' ({Description}). Signaling end-of-input to application.");

				private static readonly Action<ILogger, Exception> _waitingForSend =
					LoggerMessage.Define(LogLevel.Debug, new EventId(4, "WaitingForSend"), "Waiting for the application to finish sending data.");

				private static readonly Action<ILogger, Exception> _failedSending =
					LoggerMessage.Define(LogLevel.Debug, new EventId(5, "FailedSending"), "Application failed during sending. Sending InternalServerError close frame.");

				private static readonly Action<ILogger, Exception> _finishedSending =
					LoggerMessage.Define(LogLevel.Debug, new EventId(6, "FinishedSending"), "Application finished sending. Sending close frame.");

				private static readonly Action<ILogger, Exception> _waitingForClose =
					LoggerMessage.Define(LogLevel.Debug, new EventId(7, "WaitingForClose"), "Waiting for the client to close the socket.");

				private static readonly Action<ILogger, Exception> _closeTimedOut =
					LoggerMessage.Define(LogLevel.Debug, new EventId(8, "CloseTimedOut"), "Timed out waiting for client to send the close frame, aborting the connection.");

				private static readonly Action<ILogger, WebSocketMessageType, int, bool, Exception> _messageReceived =
					LoggerMessage.Define<WebSocketMessageType, int, bool>(LogLevel.Trace, new EventId(9, "MessageReceived"), "Message received. Type: {MessageType}, size: {Size}, EndOfMessage: {EndOfMessage}.");

				private static readonly Action<ILogger, int, Exception> _messageToApplication =
					LoggerMessage.Define<int>(LogLevel.Trace, new EventId(10, "MessageToApplication"), "Passing message to application. Payload size: {Size}.");

				private static readonly Action<ILogger, long, Exception> _sendPayload =
					LoggerMessage.Define<long>(LogLevel.Trace, new EventId(11, "SendPayload"), "Sending payload: {Size} bytes.");

				private static readonly Action<ILogger, Exception> _errorWritingFrame =
					LoggerMessage.Define(LogLevel.Error, new EventId(12, "ErrorWritingFrame"), "Error writing frame.");

				private static readonly Action<ILogger, Exception> _sendFailed =
					LoggerMessage.Define(LogLevel.Error, new EventId(13, "SendFailed"), "Socket failed to send.");

				private static readonly Action<ILogger, Exception> _closedPrematurely =
					LoggerMessage.Define(LogLevel.Debug, new EventId(14, "ClosedPrematurely"), "Socket connection closed prematurely.");

				public static void SocketOpened(ILogger logger, string subProtocol)
				{
					_socketOpened(logger, subProtocol, null);
				}

				public static void SocketClosed(ILogger logger)
				{
					_socketClosed(logger, null);
				}

				public static void ClientClosed(ILogger logger, WebSocketCloseStatus? closeStatus, string closeDescription)
				{
					_clientClosed(logger, closeStatus, closeDescription, null);
				}

				public static void WaitingForSend(ILogger logger)
				{
					_waitingForSend(logger, null);
				}

				public static void FailedSending(ILogger logger)
				{
					_failedSending(logger, null);
				}

				public static void FinishedSending(ILogger logger)
				{
					_finishedSending(logger, null);
				}

				public static void WaitingForClose(ILogger logger)
				{
					_waitingForClose(logger, null);
				}

				public static void CloseTimedOut(ILogger logger)
				{
					_closeTimedOut(logger, null);
				}

				public static void MessageReceived(ILogger logger, WebSocketMessageType type, int size, bool endOfMessage)
				{
					_messageReceived(logger, type, size, endOfMessage, null);
				}

				public static void MessageToApplication(ILogger logger, int size)
				{
					_messageToApplication(logger, size, null);
				}

				public static void SendPayload(ILogger logger, long size)
				{
					_sendPayload(logger, size, null);
				}

				public static void ErrorWritingFrame(ILogger logger, Exception ex)
				{
					_errorWritingFrame(logger, ex);
				}

				public static void SendFailed(ILogger logger, Exception ex)
				{
					_sendFailed(logger, ex);
				}

				public static void ClosedPrematurely(ILogger logger, Exception ex)
				{
					_closedPrematurely(logger, ex);
				}
			}
		}
		#endregion
	}
	#region https://github.com/aspnet/AspNetCore/blob/master/src/SignalR/common/Http.Connections/src/WebSocketOptions.cs
	public class WebSocketOptions
	{
		public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);

		/// <summary>
		/// Gets or sets a delegate that will be called when a new WebSocket is established to select the value
		/// for the 'Sec-WebSocket-Protocol' response header. The delegate will be called with a list of the protocols provided
		/// by the client in the 'Sec-WebSocket-Protocol' request header.
		/// </summary>
		/// <remarks>
		/// See RFC 6455 section 1.3 for more details on the WebSocket handshake: https://tools.ietf.org/html/rfc6455#section-1.3
		/// </remarks>
		// WebSocketManager's list of sub protocols is an IList:
		// https://github.com/aspnet/HttpAbstractions/blob/a6bdb9b1ec6ed99978a508e71a7f131be7e4d9fb/src/Microsoft.AspNetCore.Http.Abstractions/WebSocketManager.cs#L23
		// Unfortunately, IList<T> does not implement IReadOnlyList<T> :(
		public Func<IList<string>, string> SubProtocolSelector { get; set; }
	}
	#endregion
}

//namespace RSocket.Transports
//{
//	internal static partial class RSocketTransportExtensions
//	{
//		//Framing helper methods. They stay close to the original code as called, but add and remove the framing at the transport-socket boundary.
//		public static Memory<byte> GetMemory(this PipeWriter output, out Memory<byte> memoryframe) { memoryframe = output.GetMemory(); return memoryframe.Slice(sizeof(int)); }
//		public static void Advance(this PipeWriter output, int bytes, bool endOfMessage, in Memory<byte> memoryframe) { System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(memoryframe.Span, RSocketProtocol.MessageFrame(bytes, endOfMessage)); output.Advance(sizeof(int) + bytes); }
//		public static ValueTask SendAsync(this WebSocket webSocket, ReadOnlySequence<byte> buffer, SequencePosition position, WebSocketMessageType webSocketMessageType, CancellationToken cancellationToken = default)
//		{
//			buffer.TryGet(ref position, out var memory, advance: false);
//			var (length, isEndOfMessage) = RSocketProtocol.MessageFrame(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(memory.Span));
//			position = buffer.GetPosition(sizeof(int), position);
//			return webSocket.SendAsync(buffer.Slice(position, length), webSocketMessageType, cancellationToken);
//		}
//	}
//}


#region https://github.com/aspnet/AspNetCore/blob/master/src/SignalR/common/Shared/WebSocketExtensions.cs
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System.Net.WebSockets
{
	internal static class WebSocketExtensions
	{
		public static ValueTask SendAsync(this WebSocket webSocket, ReadOnlySequence<byte> buffer, WebSocketMessageType webSocketMessageType, CancellationToken cancellationToken = default)
		{
#if NETCOREAPP3_0
            if (buffer.IsSingleSegment)
            {
                return webSocket.SendAsync(buffer.First, webSocketMessageType, endOfMessage: true, cancellationToken);
            }
            else
            {
                return SendMultiSegmentAsync(webSocket, buffer, webSocketMessageType, cancellationToken);
            }
#else
			if (buffer.IsSingleSegment)
			{
				var isArray = MemoryMarshal.TryGetArray(buffer.First, out var segment);
				Debug.Assert(isArray);
				return new ValueTask(webSocket.SendAsync(segment, webSocketMessageType, endOfMessage: true, cancellationToken));
			}
			else
			{
				return SendMultiSegmentAsync(webSocket, buffer, webSocketMessageType, cancellationToken);
			}
#endif
		}

		private static async ValueTask SendMultiSegmentAsync(WebSocket webSocket, ReadOnlySequence<byte> buffer, WebSocketMessageType webSocketMessageType, CancellationToken cancellationToken = default)
		{
			var position = buffer.Start;
			// Get a segment before the loop so we can be one segment behind while writing
			// This allows us to do a non-zero byte write for the endOfMessage = true send
			buffer.TryGet(ref position, out var prevSegment);
			while (buffer.TryGet(ref position, out var segment))
			{
#if NETCOREAPP3_0
                await webSocket.SendAsync(prevSegment, webSocketMessageType, endOfMessage: false, cancellationToken);
#else
				var isArray = MemoryMarshal.TryGetArray(prevSegment, out var arraySegment);
				Debug.Assert(isArray);
				await webSocket.SendAsync(arraySegment, webSocketMessageType, endOfMessage: false, cancellationToken);
#endif
				prevSegment = segment;
			}

			// End of message frame
#if NETCOREAPP3_0
            await webSocket.SendAsync(prevSegment, webSocketMessageType, endOfMessage: true, cancellationToken);
#else
			var isArrayEnd = MemoryMarshal.TryGetArray(prevSegment, out var arraySegmentEnd);
			Debug.Assert(isArrayEnd);
			await webSocket.SendAsync(arraySegmentEnd, webSocketMessageType, endOfMessage: true, cancellationToken);
#endif
		}
	}
}
#endregion

#region LICENSE for AspNetCore
/*
                                 Apache License

						   Version 2.0, January 2004
                        http://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.

      "License" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      "Licensor" shall mean the copyright owner or entity authorized by
	  the copyright owner that is granting the License.

      "Legal Entity" shall mean the union of the acting entity and all

	  other entities that control, are controlled by, or are under common

	  control with that entity. For the purposes of this definition,
      "control" means (i) the power, direct or indirect, to cause the
	  direction or management of such entity, whether by contract or

	  otherwise, or (ii) ownership of fifty percent (50%) or more of the
	  outstanding shares, or (iii) beneficial ownership of such entity.

      "You" (or "Your") shall mean an individual or Legal Entity
	  exercising permissions granted by this License.

      "Source" form shall mean the preferred form for making modifications,
      including but not limited to software source code, documentation
	  source, and configuration files.

      "Object" form shall mean any form resulting from mechanical
	  transformation or translation of a Source form, including but
	  not limited to compiled object code, generated documentation,
      and conversions to other media types.

      "Work" shall mean the work of authorship, whether in Source or
	  Object form, made available under the License, as indicated by a
	  copyright notice that is included in or attached to the work
      (an example is provided in the Appendix below).

      "Derivative Works" shall mean any work, whether in Source or Object
	  form, that is based on (or derived from) the Work and for which the
	  editorial revisions, annotations, elaborations, or other modifications
	  represent, as a whole, an original work of authorship. For the purposes
	  of this License, Derivative Works shall not include works that remain
	  separable from, or merely link (or bind by name) to the interfaces of,
      the Work and Derivative Works thereof.

      "Contribution" shall mean any work of authorship, including
	  the original version of the Work and any modifications or additions
	  to that Work or Derivative Works thereof, that is intentionally
	  submitted to Licensor for inclusion in the Work by the copyright owner
	  or by an individual or Legal Entity authorized to submit on behalf of
	  the copyright owner. For the purposes of this definition, "submitted"
      means any form of electronic, verbal, or written communication sent
	  to the Licensor or its representatives, including but not limited to
	  communication on electronic mailing lists, source code control systems,
      and issue tracking systems that are managed by, or on behalf of, the
	  Licensor for the purpose of discussing and improving the Work, but
	  excluding communication that is conspicuously marked or otherwise
	  designated in writing by the copyright owner as "Not a Contribution."

      "Contributor" shall mean Licensor and any individual or Legal Entity
	  on behalf of whom a Contribution has been received by Licensor and
	  subsequently incorporated within the Work.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
	  copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
	  Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      (except as stated in this section) patent license to make, have made,
      use, offer to sell, sell, import, and otherwise transfer the Work,
      where such license applies only to those patent claims licensable
	  by such Contributor that are necessarily infringed by their
	  Contribution(s) alone or by combination of their Contribution(s)
      with the Work to which such Contribution(s) was submitted. If You
	  institute patent litigation against any entity (including a
	  cross-claim or counterclaim in a lawsuit) alleging that the Work
	  or a Contribution incorporated within the Work constitutes direct
	  or contributory patent infringement, then any patent licenses
	  granted to You under this License for that Work shall terminate
      as of the date such litigation is filed.

   4. Redistribution. You may reproduce and distribute copies of the
	  Work or Derivative Works thereof in any medium, with or without
	  modifications, and in Source or Object form, provided that You
	  meet the following conditions:

      (a) You must give any other recipients of the Work or
		  Derivative Works a copy of this License; and

      (b) You must cause any modified files to carry prominent notices
		  stating that You changed the files; and

      (c) You must retain, in the Source form of any Derivative Works
		  that You distribute, all copyright, patent, trademark, and
		  attribution notices from the Source form of the Work,
          excluding those notices that do not pertain to any part of
		  the Derivative Works; and

      (d) If the Work includes a "NOTICE" text file as part of its
		  distribution, then any Derivative Works that You distribute must
		  include a readable copy of the attribution notices contained
		  within such NOTICE file, excluding those notices that do not
		  pertain to any part of the Derivative Works, in at least one
		  of the following places: within a NOTICE text file distributed
          as part of the Derivative Works; within the Source form or
		  documentation, if provided along with the Derivative Works; or,
          within a display generated by the Derivative Works, if and
		  wherever such third-party notices normally appear. The contents
		  of the NOTICE file are for informational purposes only and
          do not modify the License. You may add Your own attribution
		  notices within Derivative Works that You distribute, alongside

		  or as an addendum to the NOTICE text from the Work, provided

		  that such additional attribution notices cannot be construed

		  as modifying the License.


	  You may add Your own copyright statement to Your modifications and
	  may provide additional or different license terms and conditions

	  for use, reproduction, or distribution of Your modifications, or

	  for any such Derivative Works as a whole, provided Your use,
	  reproduction, and distribution of the Work otherwise complies with

	  the conditions stated in this License.

   5.Submission of Contributions.Unless You explicitly state otherwise,
	  any Contribution intentionally submitted for inclusion in the Work

	  by You to the Licensor shall be under the terms and conditions of

	  this License, without any additional terms or conditions.

	  Notwithstanding the above, nothing herein shall supersede or modify

	  the terms of any separate license agreement you may have executed

	  with Licensor regarding such Contributions.

   6.Trademarks.This License does not grant permission to use the trade

	  names, trademarks, service marks, or product names of the Licensor,
	  except as required for reasonable and customary use in describing the

	  origin of the Work and reproducing the content of the NOTICE file.

   7.Disclaimer of Warranty.Unless required by applicable law or

	  agreed to in writing, Licensor provides the Work(and each

	  Contributor provides its Contributions) on an "AS IS" BASIS,
	  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or

	  implied, including, without limitation, any warranties or conditions

	  of TITLE, NON - INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A

	  PARTICULAR PURPOSE.You are solely responsible for determining the

	 appropriateness of using or redistributing the Work and assume any
	 risks associated with Your exercise of permissions under this License.

   8.Limitation of Liability. In no event and under no legal theory,
      whether in tort(including negligence), contract, or otherwise,
	 unless required by applicable law(such as deliberate and grossly

	  negligent acts) or agreed to in writing, shall any Contributor be

	  liable to You for damages, including any direct, indirect, special,
	  incidental, or consequential damages of any character arising as a

	  result of this License or out of the use or inability to use the

	  Work(including but not limited to damages for loss of goodwill,
	 work stoppage, computer failure or malfunction, or any and all

	 other commercial damages or losses), even if such Contributor
	 has been advised of the possibility of such damages.

   9.Accepting Warranty or Additional Liability. While redistributing

	  the Work or Derivative Works thereof, You may choose to offer,
	  and charge a fee for, acceptance of support, warranty, indemnity,
	  or other liability obligations and / or rights consistent with this

	  License.However, in accepting such obligations, You may act only

	  on Your own behalf and on Your sole responsibility, not on behalf

	  of any other Contributor, and only if You agree to indemnify,
      defend, and hold each Contributor harmless for any liability

	  incurred by, or claims asserted against, such Contributor by reason

	  of your accepting any such warranty or additional liability.

   END OF TERMS AND CONDITIONS

   APPENDIX: How to apply the Apache License to your work.


	  To apply the Apache License to your work, attach the following

	  boilerplate notice, with the fields enclosed by brackets "[]"

	  replaced with your own identifying information. (Don't include

	  the brackets!)  The text should be enclosed in the appropriate

	  comment syntax for the file format.We also recommend that a

	  file or class name and description of purpose be included on the

	  same "printed page" as the copyright notice for easier
	  identification within third-party archives.

   Copyright(c) .NET Foundation and Contributors

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

	   http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
#endregion