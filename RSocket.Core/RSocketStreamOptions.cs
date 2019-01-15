using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	public interface IRSocketStreamControl
	{
	}

	public class RSocketStreamOptions
	{
		public int InitialRequestSize { get; set; } = 10;

		public TimeSpan KeepAlive { get; set; }
		public TimeSpan Lifetime { get; set; }
		public string DataMimeType { get; set; }
		public string MetadataMimeType { get; set; }


		public static readonly RSocketStreamOptions Default = new RSocketStreamOptions()
		{
		};
	}
}
