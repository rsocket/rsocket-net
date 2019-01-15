using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RSocket.Tests
{
	[TestClass]
	public class ProtocolTests
	{
		[TestMethod]
		public void SimpleInvariantsTest()
		{
			Assert.AreEqual(0, RSocketProtocol.Header.DEFAULT_STREAM, "Default Stream should always be zero.");

			Assert.AreEqual(1, RSocketProtocol.MAJOR_VERSION, nameof(RSocketProtocol.MAJOR_VERSION));
			Assert.AreEqual(0, RSocketProtocol.MINOR_VERSION, nameof(RSocketProtocol.MINOR_VERSION));
		}

		[TestMethod]
		public void StateMachineBasicTest()
		{
		}


		[TestMethod]
		public void SetupValidationTest()
		{
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

	}
}