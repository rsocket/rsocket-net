using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RSocket.Tests
{
	[TestClass]
	public class ProtocolTests
	{
		Pipe Pipe = new Pipe();
		ReadOnlySequence<byte> Encode(string value) => new ReadOnlySequence<byte>(System.Text.Encoding.UTF8.GetBytes(value));
		string Decode(ReadOnlySequence<byte> value) => System.Text.Encoding.UTF8.GetString(value.ToArray());
		void AssertReaderEmpty() => Assert.IsFalse(Pipe.Reader.TryRead(out var readresult), "Reader not empty!");


		[TestMethod]
		public void SimpleInvariantsTest()
		{
			Assert.AreEqual(0, RSocketProtocol.Header.DEFAULT_STREAM, "Default Stream should always be zero.");
			Assert.AreEqual(1, RSocketProtocol.MAJOR_VERSION, nameof(RSocketProtocol.MAJOR_VERSION));
			Assert.AreEqual(0, RSocketProtocol.MINOR_VERSION, nameof(RSocketProtocol.MINOR_VERSION));
			AssertReaderEmpty();
		}

		[TestMethod]
		public void LeaseValidationTest()
		{
		}

		[TestMethod]
		public void KeepAliveValidationTest()
		{
		}
		[TestMethod]
		public void RequestResponseValidationTest()
		{
		}

		[TestMethod]
		public void RequestFireAndForgetValidationTest()
		{
		}

		[TestMethod]
		public void RequestStreamValidationTest()
		{
		}

		[TestMethod]
		public void RequestChannelValidationTest()
		{
		}

		[TestMethod]
		public void RequestNValidationTest()
		{
		}

		[TestMethod]
		public void CancelValidationTest()
		{
		}

		[TestMethod]
		public void PayloadValidationTest()
		{
		}

		[TestMethod]
		public void ErrorValidationTest()
		{
		}

		[TestMethod]
		public void MetadataPushValidationTest()
		{
		}

		[TestMethod]
		public void ExtensionValidationTest()
		{
		}

		[TestMethod]
		public void SetupValidationTest()
		{
			var message = Write(new RSocketProtocol.Setup(123, 456, nameof(RSocketProtocol.Setup.MetadataMimeType), nameof(RSocketProtocol.Setup.DataMimeType)));
			var actual = ReadSetup();
			Assert.AreEqual(message.MajorVersion, actual.MajorVersion, nameof(message.MajorVersion));
			Assert.AreEqual(message.MinorVersion, actual.MinorVersion, nameof(message.MinorVersion));
			Assert.AreEqual(message.KeepAlive, actual.KeepAlive, nameof(message.KeepAlive));
			Assert.AreEqual(message.Lifetime, actual.Lifetime, nameof(message.Lifetime));
			CollectionAssert.AreEqual((message.ResumeToken ?? new byte[0]), actual.ResumeToken, nameof(message.ResumeToken));
			Assert.AreEqual(message.MetadataMimeType, actual.MetadataMimeType, nameof(message.MetadataMimeType));
			Assert.AreEqual(message.DataMimeType, actual.DataMimeType, nameof(message.DataMimeType));
			AssertReaderEmpty();
		}

		[TestMethod]
		public void SetupWithDataMetadataTest()
		{
			var test = (metadata: Encode("Test Metadata"), data: Encode("Test Data"));

			Write(new RSocketProtocol.Setup(123, 456, nameof(RSocketProtocol.Setup.MetadataMimeType), nameof(RSocketProtocol.Setup.DataMimeType)));
			var Neither = ReadSetup(out var metadata, out var data);
			Assert.AreEqual(string.Empty, Decode(metadata), $"{nameof(Neither)} {nameof(metadata)} mismatch!");
			Assert.AreEqual(string.Empty, Decode(data), $"{nameof(Neither)} {nameof(data)} mismatch");
			AssertReaderEmpty();

			Write(new RSocketProtocol.Setup(123, 456, nameof(RSocketProtocol.Setup.MetadataMimeType), nameof(RSocketProtocol.Setup.DataMimeType), data: test.data), data: test.data);
			var DataOnly = ReadSetup(out metadata, out data);
			Assert.AreEqual(string.Empty, Decode(metadata), $"{nameof(DataOnly)} {nameof(metadata)} mismatch!");
			Assert.AreEqual(Decode(test.data), Decode(data), $"{nameof(DataOnly)} {nameof(data)} mismatch");
			AssertReaderEmpty();

			Write(new RSocketProtocol.Setup(123, 456, nameof(RSocketProtocol.Setup.MetadataMimeType), nameof(RSocketProtocol.Setup.DataMimeType), metadata: test.metadata), metadata: test.metadata);
			var MetadataOnly = ReadSetup(out metadata, out data);
			Assert.AreEqual(Decode(test.metadata), Decode(metadata), $"{nameof(MetadataOnly)} {nameof(metadata)} mismatch!");
			Assert.AreEqual(string.Empty, Decode(data), $"{nameof(MetadataOnly)} {nameof(data)} mismatch");
			AssertReaderEmpty();

			Write(new RSocketProtocol.Setup(123, 456, nameof(RSocketProtocol.Setup.MetadataMimeType), nameof(RSocketProtocol.Setup.DataMimeType), metadata: test.metadata, data: test.data), metadata: test.metadata, data: test.data);
			var Both = ReadSetup(out metadata, out data);
			Assert.AreEqual(Decode(test.metadata), Decode(metadata), $"{nameof(Both)} {nameof(metadata)} mismatch!");
			Assert.AreEqual(Decode(test.data), Decode(data), $"{nameof(Both)} {nameof(data)} mismatch");
			AssertReaderEmpty();
		}


		#region Read/Write Helpers
		RSocketProtocol.Setup ReadSetup() => ReadSetup(out var metadata, out var data);
		RSocketProtocol.Setup ReadSetup(out ReadOnlySequence<byte> metadata, out ReadOnlySequence<byte> data) { var result = new RSocketProtocol.Setup(Read(out var reader, RSocketProtocol.Types.Setup), ref reader); result.Read(ref reader, out metadata, out data); Pipe.Reader.AdvanceTo(reader.Position); return result; }

		RSocketProtocol.Header Read(out SequenceReader<byte> reader, RSocketProtocol.Types type)
		{
			Assert.IsTrue(Pipe.Reader.TryRead(out var result), "Failed to read Pipe.");
			var (Length, IsEndOfMessage) = RSocketProtocol.MessageFramePeek(result.Buffer);
			reader = new SequenceReader<byte>(result.Buffer.Slice(RSocketProtocol.MESSAGEFRAMESIZE, Length));
			var header = new RSocketProtocol.Header(ref reader, Length);
			Assert.AreEqual(type, header.Type, "Incorrect Message Type");
			return header;
		}

		RSocketProtocol.Setup Write(RSocketProtocol.Setup message, ReadOnlySequence<byte> data = default, ReadOnlySequence<byte> metadata = default) { message.WriteFlush(Pipe.Writer, data: data, metadata: metadata).Wait(); return message; }
		//void Write(RSocketProtocol.Lease message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.KeepAlive message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.RequestResponse message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.RequestFireAndForget message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.RequestStream message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.RequestChannel message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.RequestN message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.Cancel message) => message.WriteFlush(Writer).Wait();
		void Write(RSocketProtocol.Payload message) => message.WriteFlush(Pipe.Writer).Wait();
		//void Write(RSocketProtocol.Error message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.MetadataPush message) => message.WriteFlush(Writer).Wait();
		//void Write(RSocketProtocol.Extension message) => message.WriteFlush(Writer).Wait();
		#endregion
	}
}
