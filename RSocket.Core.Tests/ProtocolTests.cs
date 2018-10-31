using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RSocket.Core.Tests
{
	[TestClass]
	public class ProtocolTests
	{
		[TestMethod]
		public void DependencyCheckTest()
		{
			Assert.AreEqual(0, RSocketProtocol.Header.DEFAULT_STREAM, "Default Stream should always be zero.");
		}

		//TODO Paste in previous test suite.
	}
}
