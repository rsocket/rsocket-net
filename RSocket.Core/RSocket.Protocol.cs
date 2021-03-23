using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using IRSocketStream = System.IObserver<RSocket.Payload>;
using System.Reactive.Disposables;
using System.Reactive.Threading.Tasks;

namespace RSocket
{
	public partial class RSocket : IRSocketProtocol
	{
		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), ValueTask<Payload>> Responder { get; set; } = request => throw new NotImplementedException();
		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<Payload>> Streamer { get; set; } = request => throw new NotImplementedException();
		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IPublisher<Payload>, IObservable<Payload>> Channeler { get; set; } = (request, incoming) => throw new NotImplementedException();

		void MessageDispatch(int streamId, Action<IFrameHandler> act)
		{
			if (this.FrameHandlerDispatcher.TryGetValue(streamId, out var frameHandler))
			{
				act(frameHandler);
			}
			else
			{
#if DEBUG
				Console.WriteLine($"missing handler: {streamId}");
#endif
				//TODO Log missing handler here.
			}
		}
		async Task ExecuteFrameHandler(int streamId, IFrameHandler frameHandler)
		{
			this.FrameHandlerDispatch(streamId, frameHandler);
			try
			{
				await frameHandler.ToTask();
			}
			finally
			{
				this.FrameHandlerRemove(streamId);
				frameHandler.Dispose();
#if DEBUG
				Console.WriteLine($"Responder.frameHandler.Dispose(): stream[{streamId}]");
#endif
			}
		}

		public virtual void Setup(RSocketProtocol.Setup value)
		{
			//TODO This exception just stalls processing. Need to make sure it's handled.
			throw new InvalidOperationException($"Client cannot process Setup frames");
		}

		void IRSocketProtocol.KeepAlive(RSocketProtocol.KeepAlive value)
		{
			this._lastKeepAliveReceived = DateTime.Now;

			if (value.Respond)
			{
				RSocketProtocol.KeepAlive keepAlive = new RSocketProtocol.KeepAlive(0, false);
				keepAlive.WriteFlush(this.Transport.Output);
			}
		}

		void IRSocketProtocol.Error(RSocketProtocol.Error message)
		{
			if (message.Stream > 0)
			{
				this.MessageDispatch(message.Stream, handler =>
				{
					handler.HandleError(message);
				});

				return;
			}

			switch (message.ErrorCode)
			{
				case RSocketProtocol.ErrorCodes.Rejected_Setup:
				case RSocketProtocol.ErrorCodes.Unsupported_Setup:
				case RSocketProtocol.ErrorCodes.Invalid_Setup:
					{
						this.CloseConnection().Wait();
					}
					break;
				case RSocketProtocol.ErrorCodes.Connection_Error:
					{
						this.CloseConnection().Wait();
						this.ReleaseAllFrameHandlers(message.ErrorCode, message.ErrorText);
					}
					break;
				case RSocketProtocol.ErrorCodes.Connection_Close:
					{
						this.ReleaseAllFrameHandlers(message.ErrorCode, message.ErrorText);
						this.CloseConnection().Wait();
					}
					break;
				default:
					throw new NotImplementedException();
			}
		}

		void IRSocketProtocol.Payload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.MessageDispatch(message.Stream, handler =>
			{
				handler.HandlePayload(message, metadata, data);
			});
		}

		void IRSocketProtocol.RequestN(RSocketProtocol.RequestN message)
		{
			this.MessageDispatch(message.Stream, handler =>
			{
				handler.HandleRequestN(message);
			});
		}

		void IRSocketProtocol.Cancel(RSocketProtocol.Cancel message)
		{
			this.MessageDispatch(message.Stream, handler =>
			{
				handler.HandleCancel(message);
			});
		}


		void IRSocketProtocol.RequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.HandleRequestFireAndForget(message, metadata, data);
		}
		public virtual void HandleRequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			throw new NotImplementedException();
		}

		void IRSocketProtocol.RequestResponse(RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Schedule(message.Stream, async (stream, cancel) =>
			{
				RequestResponseResponderFrameHandler frameHandler = new RequestResponseResponderFrameHandler(this, stream, metadata, data);
				await this.ExecuteFrameHandler(message.Stream, frameHandler);
			});
		}

		void IRSocketProtocol.RequestStream(RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Schedule(message.Stream, async (stream, cancel) =>
			{
				Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<Payload>, IObservable<Payload>> channeler = (request, incoming) =>
				{
					var outgoing = this.Streamer((data, metadata));
					incoming.Subscribe(a => { });
					return outgoing;
				};

				RequestStreamResponderFrameHandler frameHandler = new RequestStreamResponderFrameHandler(this, stream, metadata, data, message.InitialRequest, channeler);

				await this.ExecuteFrameHandler(message.Stream, frameHandler);
			});
		}

		void IRSocketProtocol.RequestChannel(RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Schedule(message.Stream, async (stream, cancel) =>
			{
				ResponderFrameHandler frameHandler = new ResponderFrameHandler(this, stream, metadata, data, message.InitialRequest, this.Channeler);
				await this.ExecuteFrameHandler(message.Stream, frameHandler);
			});
		}
	}
}