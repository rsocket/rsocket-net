using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using System.Buffers.Binary;

namespace RSocket
{
	using System.Threading;
	using Microsoft.AspNetCore.Internal;

	public partial class RSocketProtocol
	{
		//Memory Buffer Builders: ArrayPool<byte>
		//https://github.com/aspnet/SignalR/blob/2d4fb0af6fd2ef2e3a059a2a15a56484d8800d35/src/Common/MemoryBufferWriter.cs


		//TODO METADATA??	String or Binary..?
		//TODO MetadataPresent in Header Constructor...? Maybe Ignore too for consistency.
		//TODO Data (/Metadata?) AsSpan()
		//TODO StreamIds round-tripping.
		//TODO IBufferWriter...

		//TODO Consider Flag Tests on this:
		//COMPLETE: 0x40, // PAYLOAD, REQUEST_CHANNEL: indicates stream completion, if set onComplete will be invoked on receiver.
		//FOLLOWS: 0x80, // (unused)
		//IGNORE: 0x200, // (all): Ignore frame if not understood.
		//LEASE: 0x40, // SETUP: Will honor lease or not.
		//METADATA: 0x100, // (all): must be set if metadata is present in the frame.
		//NEXT: 0x20, // PAYLOAD: indicates data/metadata present, if set onNext will be invoked on receiver.
		//RESPOND: 0x80, // KEEPALIVE: should KEEPALIVE be sent by peer on receipt.
		//RESUME_ENABLE: 0x80, // SETUP: Client requests resume capability if possible. Resume Identification Token present.


		public ref struct Test
		{
			public string Text;

			public Test(string text)
			{
				Text = text;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				writer.Write(Text);
			}
		}


		public ref struct Extension
		{
			public const ushort FLAG_IGNORE = 0b_____10_00000000;
			public const ushort FLAG_METADATA = 0b___01_00000000;

			public Header Header;
			public Int32 ExtendedType;

			public Extension(Int32 extendedtype, bool ignore = false, bool metadatapresent = false)
			{
				Header = new Header(Types.Extension);
				ExtendedType = extendedtype;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				writer.WriteInt32BigEndian(ExtendedType);
			}
		}


		public ref struct MetadataPush
		{
			public const ushort FLAG_METADATA = 0b___01_00000000;

			public Header Header;
			public string Metadata;
			bool HasMetadata => !string.IsNullOrEmpty(Metadata);

			public MetadataPush(string metadata)
			{
				Header = new Header(Types.Metadata_Push, flags: FLAG_METADATA);
				Metadata = metadata;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				if (HasMetadata) { writer.Write(Metadata); }
			}
		}



		public ref struct Payload
		{
			public const ushort FLAG_METADATA = 0b___01_00000000;
			public const ushort FLAG_FOLLOWS = 0b____00_10000000;
			public const ushort FLAG_COMPLETE = 0b___00_01000000;
			public const ushort FLAG_NEXT = 0b_______00_00100000;
			public bool MetadataPresent { get => (Header.Flags & FLAG_METADATA) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_METADATA) : (ushort)(Header.Flags & ~FLAG_METADATA); }
			public bool Follows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }
			public bool Complete { get => (Header.Flags & FLAG_COMPLETE) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_COMPLETE) : (ushort)(Header.Flags & ~FLAG_COMPLETE); }
			public bool Next { get => (Header.Flags & FLAG_NEXT) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_NEXT) : (ushort)(Header.Flags & ~FLAG_NEXT); }

			public Header Header;
			public string Metadata;
			public byte[] Data;
			bool HasMetadata => !string.IsNullOrEmpty(Metadata);
			bool HasData => Data != null && Data.Length > 0;

			public Payload(byte[] data, string metadata = null, bool follows = false, bool complete = false, bool next = false)
			{
				Header = new Header(Types.Request_Channel);
				Metadata = metadata;
				Data = data;
				MetadataPresent = HasMetadata;
				Follows = follows;
				Complete = complete;
				Next = next;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				if (HasMetadata) { writer.Write(Metadata); }
				if (HasData) { writer.Write(Data); }
			}
		}



		public ref struct Cancel
		{
			public Header Header;

			public Cancel(Int32 request)
			{
				Header = new Header(Types.Cancel);
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
			}
		}


		public ref struct RequestN
		{
			public Header Header;
			public Int32 Request;

			public RequestN(Int32 request)
			{
				Header = new Header(Types.Request_N);
				Request = request; //TODO MUST be > 0
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				writer.WriteInt32BigEndian(Request);
			}
		}


		public ref struct RequestChannel
		{
			public const ushort FLAG_METADATA = 0b___01_00000000;
			public const ushort FLAG_FOLLOWS = 0b____00_10000000;
			public const ushort FLAG_COMPLETE = 0b___00_01000000;
			public bool MetadataPresent { get => (Header.Flags & FLAG_METADATA) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_METADATA) : (ushort)(Header.Flags & ~FLAG_METADATA); }
			public bool Follows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }
			public bool Complete { get => (Header.Flags & FLAG_COMPLETE) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_COMPLETE) : (ushort)(Header.Flags & ~FLAG_COMPLETE); }

			public Header Header;
			public Int32 InitialRequest;
			public string Metadata;
			public byte[] Data;
			bool HasMetadata => !string.IsNullOrEmpty(Metadata);
			bool HasData => Data != null && Data.Length > 0;

			public RequestChannel(Int32 initialRequest, byte[] data, string metadata = null, bool follows = false, bool complete = false)
			{
				Header = new Header(Types.Request_Channel);
				InitialRequest = initialRequest;            //TODO MUST be > 0
				Metadata = metadata;
				Data = data;
				MetadataPresent = HasMetadata;
				Follows = follows;
				Complete = complete;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				writer.WriteInt32BigEndian(InitialRequest);
				if (HasMetadata) { writer.Write(Metadata); }
				if (HasData) { writer.Write(Data); }
			}
		}



		public ref struct RequestStream
		{
			public const ushort FLAG_METADATA = 0b__01_00000000;
			public const ushort FLAG_FOLLOWS = 0b___00_10000000;
			public bool MetadataPresent { get => (Header.Flags & FLAG_METADATA) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_METADATA) : (ushort)(Header.Flags & ~FLAG_METADATA); }
			public bool Follows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }

			public Header Header;
			public Int32 InitialRequest;
			public string Metadata;
			public byte[] Data;
			public string StringData;
			bool HasMetadata => !string.IsNullOrEmpty(Metadata);
			bool HasData => Data != null && Data.Length > 0;
			bool HasStringData => !string.IsNullOrEmpty(StringData);


			public RequestStream(Int32 initialRequest, string data, string metadata = null, bool follows = false)
			{
				Header = new Header(Types.Request_Stream);
				InitialRequest = initialRequest;        //TODO MUST be > 0
				Data = null;
				StringData = data;
				Metadata = metadata;
				MetadataPresent = HasMetadata;
				Follows = follows;
			}

			public RequestStream(Int32 initialRequest, byte[] data, string metadata = null, bool follows = false)
			{
				Header = new Header(Types.Request_Stream);
				InitialRequest = initialRequest;		//TODO MUST be > 0
				Data = data;
				StringData = null;
				Metadata = metadata;
				MetadataPresent = HasMetadata;
				Follows = follows;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				writer.WriteInt32BigEndian(InitialRequest);
				if (HasMetadata) { writer.Write(Metadata); }
				if (HasData) { writer.Write(Data); }
				if (HasStringData) { writer.Write(StringData); }
			}
		}

		public ref struct RequestFireAndForget
		{
			public const ushort FLAG_METADATA = 0b__01_00000000;
			public const ushort FLAG_FOLLOWS = 0b___00_10000000;
			public bool MetadataPresent { get => (Header.Flags & FLAG_METADATA) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_METADATA) : (ushort)(Header.Flags & ~FLAG_METADATA); }
			public bool Follows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }

			public Header Header;
			public string Metadata;
			public byte[] Data;
			bool HasMetadata => !string.IsNullOrEmpty(Metadata);
			bool HasData => Data != null && Data.Length > 0;

			public RequestFireAndForget(byte[] data, string metadata = null, bool follows = false)
			{
				Header = new Header(Types.Request_Fire_And_Forget);
				Metadata = metadata;
				Data = data;
				MetadataPresent = HasMetadata;
				Follows = follows;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				if (HasMetadata) { writer.Write(Metadata); }
				if (HasData) { writer.Write(Data); }
			}
		}


		public ref struct RequestResponse
		{
			public const ushort FLAG_METADATA = 0b__01_00000000;
			public const ushort FLAG_FOLLOWS = 0b___00_10000000;
			public bool MetadataPresent { get => (Header.Flags & FLAG_METADATA) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_METADATA) : (ushort)(Header.Flags & ~FLAG_METADATA); }
			public bool Follows { get => (Header.Flags & FLAG_FOLLOWS) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_FOLLOWS) : (ushort)(Header.Flags & ~FLAG_FOLLOWS); }

			public Header Header;
			public string Metadata;
			public byte[] Data;
			bool HasMetadata => !string.IsNullOrEmpty(Metadata);
			bool HasData => Data != null && Data.Length > 0;

			public RequestResponse(byte[] data, string metadata = null, bool follows = false)
			{
				Header = new Header(Types.Request_Response);
				Metadata = metadata;
				Data = data;
				MetadataPresent = HasMetadata;
				Follows = follows;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				if (HasMetadata) { writer.Write(Metadata); }
				if (HasData) { writer.Write(Data); }
			}
		}


		public ref struct KeepAlive
		{
			public const ushort FLAG_RESPOND = 0b__00_10000000;
			public bool Respond { get => (Header.Flags & FLAG_RESPOND) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_RESPOND) : (ushort)(Header.Flags & ~FLAG_RESPOND); }

			public Header Header;
			public Int32 LastReceivedPosition;
			public byte[] Data;
			bool HasData => Data != null && Data.Length > 0;

			public KeepAlive(Int32 lastReceivedPosition, bool respond, byte[] data = null)
			{
				Header = new Header(Types.KeepAlive);
				LastReceivedPosition = lastReceivedPosition;
				Data = data;
				Respond = respond;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				writer.WriteInt32BigEndian(LastReceivedPosition);
				if (HasData) { writer.Write(Data); }
			}
		}


		public ref struct Lease
		{
			public const ushort FLAG_METADATA = 0b__01_00000000;

			public Header Header;
			public bool MetadataPresent { get => (Header.Flags & FLAG_METADATA) != 0; set => Header.Flags = value ? (ushort)(Header.Flags | FLAG_METADATA) : (ushort)(Header.Flags & ~FLAG_METADATA); }
			public Int32 TimeToLive;
			public Int32 NumberOfRequests;
			public string Metadata;
			bool HasMetadata => !string.IsNullOrEmpty(Metadata);

			public Lease(Int32 timeToLive, Int32 numberOfRequests, string metadata)
			{
				Header = new Header(Types.Lease);
				TimeToLive = timeToLive;
				NumberOfRequests = numberOfRequests;
				Metadata = metadata;
				MetadataPresent = HasMetadata;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				writer.WriteInt32BigEndian(TimeToLive);
				writer.WriteInt32BigEndian(NumberOfRequests);
				if (HasMetadata) { writer.Write(Metadata); }
			}
		}


		public ref struct Setup
		{
			public const ushort FLAG_METADATA = 0b__01_00000000;
			public const ushort FLAG_RESUME = 0b____00_10000000;
			public const ushort FLAG_LEASE = 0b_____00_01000000;

			public Header Header;
			public const UInt16 MajorVersion = MAJOR_VERSION;
			public const UInt16 MinorVersion = MINOR_VERSION;
			public Int32 KeepAlive;
			public Int32 Lifetime;
			public byte[] ResumeToken;
			public string MetadataMimeType;
			public string DataMimeType;

			//public const int Length = Header.LENGTH + sizeof(ErrorCodes);	= 46

			//TODO Resume/ResumeToken, Length, Lease

			public Setup(TimeSpan keepalive, TimeSpan lifetime, string metadataMimeType = null, string dataMimeType = null) : this((int)keepalive.TotalMilliseconds, (int)lifetime.TotalMilliseconds, string.IsNullOrEmpty(metadataMimeType) ? string.Empty : metadataMimeType, string.IsNullOrEmpty(dataMimeType) ? string.Empty : dataMimeType) { }

			public Setup(Int32 keepalive, Int32 lifetime, string metadataMimeType, string dataMimeType, byte[] resumeToken = null)
			{
				Header = new Header(Types.Setup, flags: (resumeToken != null) ? FLAG_RESUME : 0);
				KeepAlive = keepalive;
				Lifetime = lifetime;
				MetadataMimeType = metadataMimeType;
				DataMimeType = dataMimeType;
				ResumeToken = resumeToken;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);   //TODO ResumeToken & Length (Track in Writer?)
				writer.WriteUInt16BigEndian(MajorVersion);
				writer.WriteUInt16BigEndian(MinorVersion);
				writer.WriteInt32BigEndian(KeepAlive);
				writer.WriteInt32BigEndian(Lifetime);
				if (ResumeToken != null) { writer.WriteUInt16BigEndian(ResumeToken.Length); writer.Write(ResumeToken); }
				writer.WritePrefixByte(MetadataMimeType);
				writer.WritePrefixByte(DataMimeType);
			}
		}



		public ref struct Error
		{
			public Header Header;
			public ErrorCodes ErrorCode;
			public string Data;
			//public const int Length = Header.LENGTH + sizeof(ErrorCodes);

			public Error(ErrorCodes code, string data = null, Int32 stream = Header.DEFAULT_STREAM)
			{
				Header = new Header(Types.Error, stream);
				ErrorCode = code;
				Data = string.IsNullOrEmpty(data) ? string.Empty : data;
			}

			public void Write(PipeWriter pipe) { var writer = BufferWriter.Get(pipe); this.Write(writer); writer.Flush(); BufferWriter.Return(writer); }

			void Write(BufferWriter writer)
			{
				Header.Write(writer);
				writer.WriteInt32BigEndian((int)ErrorCode);
				writer.Write(Data);
			}
		}

		public ref struct Header
		{
			public const Int32 DEFAULT_STREAM = 0;
			internal const int FRAMETYPE_OFFSET = 10;
			internal const ushort FRAMETYPE_TYPE = 0b_111111 << FRAMETYPE_OFFSET;
			internal const ushort FLAGS = 0b__________11_11111111;
			internal const ushort FLAG_IGNORE = 0b____10_00000000;
			internal const ushort FLAG_METADATA = 0b__01_00000000;

			public Int32 Stream;
			public Types Type;
			//public Boolean Ignore;
			//public Boolean Metadata;
			public UInt16 Flags;
			public const int LENGTH = sizeof(Int32) + sizeof(Int16);


			public Header(Types type, Int32 stream = 0, int flags = 0)
			{
				Type = type;
				Stream = stream;
				Flags = (UInt16)flags;
			}

			public void Write(BufferWriter writer)
			{
				//if (length > 0) { writer.WriteInt24BigEndian(length); }
				writer.WriteInt32BigEndian(Stream);
				writer.WriteUInt16BigEndian((((int)Type << FRAMETYPE_OFFSET) & FRAMETYPE_TYPE) | (Flags & FLAGS));//  (Ignore ? FLAG_IGNORE : 0) | (Metadata ? FLAG_METADATA : 0));
			}
		}
	}
}
