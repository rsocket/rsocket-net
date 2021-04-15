using RSocket.Channels;
using System;
using System.Buffers;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace RSocket
{
	public partial class RSocket : IRSocketProtocol
	{
		/// <summary>
		/// Called by socket thread, so this method affects how efficiently the thread receives data.
		/// </summary>
		public Action<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata)> FireAndForgetHandler { get; set; } = request => throw new NotImplementedException();
		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), ValueTask<Payload>> Responder { get; set; } = request => throw new NotImplementedException();
		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<Payload>/*You can return an IPublisher<T> object which implements backpressure*/> Streamer { get; set; } = request => throw new NotImplementedException();
		public Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IPublisher<Payload>, IObservable<Payload>/*You can return an IPublisher<T> object which implements backpressure*/> Channeler { get; set; } = (request, incoming) => throw new NotImplementedException();

		void MessageDispatch(int streamId, Action<IChannel> act)
		{
			if (this._activeChannels.TryGetValue(streamId, out var channel))
			{
				act(channel);
			}
			else
			{
#if DEBUG
				Console.WriteLine($"missing frame handler: {streamId}");
#endif
				//TODO Log missing frame handler here.
			}
		}
		async Task ChannelDispatch(IChannel channel)
		{
			int channelId = channel.ChannelId;
			this.AddChannel(channel);
			try
			{
				/*
				 * Using a new task to run the channel in case blocking socket thread.
				 */
				await Task.Run(async () =>
				{
					await channel.ToTask();
				});
			}
			catch (Exception ex)
			{
#if DEBUG
				Console.WriteLine($"error: stream[{channelId}] {ex.Message} {ex.StackTrace}");
#endif
			}
			finally
			{
				this.RemoveChannel(channelId);
				channel.Dispose();
#if DEBUG
				Console.WriteLine($"----------------Channel of responder has terminated: stream[{channelId}]----------------");
#endif
			}
		}

		void IRSocketProtocol.Setup(RSocketProtocol.Setup message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this.HandleSetup(message, metadata, data);
		}
		protected virtual void HandleSetup(RSocketProtocol.Setup message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			//TODO This exception just stalls processing. Need to make sure it's handled.
			throw new InvalidOperationException($"Client cannot process Setup frames");
		}

		void IRSocketProtocol.KeepAlive(RSocketProtocol.KeepAlive value)
		{
			this._lastKeepAliveReceived = DateTime.Now;
			if (value.Respond)
			{
				this.SendKeepAlive(0, false);
			}
		}

		void IRSocketProtocol.Error(RSocketProtocol.Error message)
		{
#if DEBUG
			Console.WriteLine($"Handling error message[{Enum.GetName(message.ErrorCode.GetType(), message.ErrorCode)}]...............stream[{message.Stream}]");
#endif

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
						this.ReleaseAllChannels(message.ErrorCode, message.ErrorText);
					}
					break;
				case RSocketProtocol.ErrorCodes.Connection_Close:
					{
						this.ReleaseAllChannels(message.ErrorCode, message.ErrorText);
						this.CloseConnection().Wait();
					}
					break;
				default:
					throw new NotImplementedException();
			}
		}

		void IRSocketProtocol.Payload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			data = data.Clone();
			metadata = metadata.Clone();
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
			data = data.Clone();
			metadata = metadata.Clone();
			this.FireAndForgetHandler((data, metadata));
		}

		void IRSocketProtocol.RequestResponse(RSocketProtocol.RequestResponse message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			data = data.Clone();
			metadata = metadata.Clone();
			Schedule(message.Stream, async (stream, cancel) =>
			{
				RequestResponseResponderChannel channel = new RequestResponseResponderChannel(this, stream, metadata, data);
				await this.ChannelDispatch(channel);
			});
		}

		void IRSocketProtocol.RequestStream(RSocketProtocol.RequestStream message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			data = data.Clone();
			metadata = metadata.Clone();
			Schedule(message.Stream, async (stream, cancel) =>
			{
				Func<(ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata), IObservable<Payload>, IObservable<Payload>> channeler = (request, incoming) =>
				{
					var outgoing = this.Streamer((data, metadata));
					return outgoing;
				};

				RequestStreamResponderChannel channel = new RequestStreamResponderChannel(this, stream, metadata, data, message.InitialRequest, channeler);
				await this.ChannelDispatch(channel);
			});
		}

		void IRSocketProtocol.RequestChannel(RSocketProtocol.RequestChannel message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			/*
			 * If the `ReadOnlySequence<byte>` object is from socket buffer, it will appear exception when executing `ReadOnlySequence<byte>.ToArray` method in frame handler in some cases. It doesn't always come up and i don't know why!!! So i create a new `ReadOnlySequence<byte>` object as a temporary solution.
			 * The exception:
			 * Specified argument was out of the range of valid values.
			   Parameter name: start
			   at System.ReadOnlyMemory`1.Slice(Int32 start, Int32 length)
               at System.Buffers.BuffersExtensions.ToArray[T](ReadOnlySequence`1& sequence)
			   at RSocketDemo.RSocketDemoServer.ForRequestChannel(ValueTuple`2 request, IPublisher`1
			 */
			data = data.Clone();
			metadata = metadata.Clone();
			Schedule(message.Stream, async (stream, cancel) =>
			{
				ResponderChannel channel = new ResponderChannel(this, stream, metadata, data, message.InitialRequest, this.Channeler);
				await this.ChannelDispatch(channel);
			});
		}
	}
}
