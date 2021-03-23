using RSocket.Exceptions;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	partial class RSocket
	{
		static void Decoded(string message) => Console.WriteLine(message);

		async Task Handler(PipeReader pipereader, CancellationToken cancellation)
		{
			//The original implementation was a state-machine parser with resumability. It doesn't seem like the other implementations follow this pattern and the .NET folks are still figuring this out too - see the internal JSON parser discussion for how they're handling state machine persistence across async boundaries when servicing a Pipeline. So, this version of the handler only processes complete messages at some cost to memory buffer scalability.
			//Note that this means that the Pipeline must be configured to have enough buffering for a complete message before source-quenching. This also means that the downstream consumers don't really have to support resumption, so the interface no longer has the partial buffer methods in it.
			while (!cancellation.IsCancellationRequested)
			{
				var read = await pipereader.ReadAsync(cancellation);
				var buffer = read.Buffer;
				if (buffer.IsEmpty && read.IsCompleted) { break; }
				var position = buffer.Start;

				//Due to the nature of Pipelines as simple binary pipes, all Transport adapters assemble a standard message frame whether or not the underlying transport signals length, EoM, etc.
				var (Length, IsEndOfMessage) = RSocketProtocol.MessageFramePeek(buffer);
				if (buffer.Length < Length + RSocketProtocol.MESSAGEFRAMESIZE)
				{
					pipereader.AdvanceTo(buffer.Start, buffer.End);
					continue; //Don't have a complete message yet. Tell the pipe that we've evaluated up to the current buffer end, but cannot yet consume it.
				}

				var sequence = buffer.Slice(position = buffer.GetPosition(RSocketProtocol.MESSAGEFRAMESIZE, position), Length);

				try
				{
					await Process(Length, sequence);
				}
				catch (RSocketErrorException ex)
				{
					var errorData = Helpers.StringToByteSequence(ex.Message);
					Error error = new Error(ex.ErrorCode, 0, errorData, ex.Message);
					await error.WriteFlush(this.Transport.Output, errorData);
					await this.CloseConnection();
					break;
				}
				catch (Exception ex)
				{
#if DEBUG
					Console.WriteLine($"An exception occurred when processing message: {ex.Message} {ex.StackTrace}");
#endif
				}
				pipereader.AdvanceTo(position = buffer.GetPosition(Length, position));
				//TODO UNIT TEST- this should work now too!!! Need to evaluate if there is more than one packet in the pipe including edges like part of the length bytes are there but not all.
			}

			pipereader.Complete();


			//This is the non-async portion of the handler. SequenceReader<T> and the other stack-allocated items cannot be used in an async context.
			Task Process(int framelength, ReadOnlySequence<byte> sequence)
			{
				var reader = new SequenceReader<byte>(sequence);
				var header = new Header(ref reader, framelength);
				return this.Process(header, reader);
			}
		}

		protected virtual Task Process(Header header, SequenceReader<byte> reader)
		{
			IRSocketProtocol sink = this;

			switch (header.Type)
			{
				case Types.Reserved: throw new InvalidOperationException($"Protocol Reserved! [{header.Type}]");
				case Types.Lease:
					var lease = new Lease(header, ref reader);
					break;
				case Types.KeepAlive:
					var keepalive = new KeepAlive(header, ref reader);
					sink.KeepAlive(keepalive);
					break;
				case Types.Request_Response:
					var requestresponse = new RequestResponse(header, ref reader);
					if (requestresponse.Validate()) { sink.RequestResponse(requestresponse, requestresponse.ReadMetadata(reader), requestresponse.ReadData(reader)); }
					break;
				case Types.Request_Fire_And_Forget:
					var requestfireandforget = new RequestFireAndForget(header, ref reader);
					if (requestfireandforget.Validate()) { sink.RequestFireAndForget(requestfireandforget, requestfireandforget.ReadMetadata(reader), requestfireandforget.ReadData(reader)); }
					break;
				case Types.Request_Stream:
					var requeststream = new RequestStream(header, ref reader);
					if (requeststream.Validate()) { sink.RequestStream(requeststream, requeststream.ReadMetadata(reader), requeststream.ReadData(reader)); }
					break;
				case Types.Request_Channel:
					var requestchannel = new RequestChannel(header, ref reader);
					if (requestchannel.Validate()) { sink.RequestChannel(requestchannel, requestchannel.ReadMetadata(reader), requestchannel.ReadData(reader)); }
					break;
				case Types.Request_N:
					var requestne = new RequestN(header, ref reader);
					if (requestne.Validate()) { sink.RequestN(requestne); }
					break;
				case Types.Cancel:
					var cancel = new Cancel(header, ref reader);
					if (cancel.Validate()) { sink.Cancel(cancel); }
					break;
				case Types.Payload:
					var payload = new RSocketProtocol.Payload(header, ref reader);
#if DEBUG
					Decoded(payload.ToString());
#endif
					if (payload.Validate())
					{
						sink.Payload(payload, payload.ReadMetadata(reader), payload.ReadData(reader));
					}
					break;
				case Types.Error:
					var error = new Error(header, ref reader);
#if DEBUG
					Decoded(error.ToString());
#endif
					sink.Error(error);
					break;
				case Types.Metadata_Push:
					var metadatapush = new MetadataPush(header, ref reader);
					break;
				case Types.Resume: { throw new NotSupportedException($"Protocol Resumption not Supported. [{header.Type}]"); }
				case Types.Resume_OK: { throw new NotSupportedException($"Protocol Resumption not Supported. [{header.Type}]"); }
				case Types.Extension: if (!header.CanIgnore) { throw new InvalidOperationException($"Protocol Extension Unsupported! [{header.Type}]"); } else break;
				default: if (!header.CanIgnore) { throw new InvalidOperationException($"Protocol Unknown Type! [{header.Type}]"); } else break;
			}
			return Task.CompletedTask;
		}
	}
}
