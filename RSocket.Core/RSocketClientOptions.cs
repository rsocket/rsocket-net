using System;

namespace RSocket
{
	public class RSocketClientOptions
	{
		public const string BINARYMIMETYPE = "binary";

		public TimeSpan KeepAlive { get; set; }
		public TimeSpan Lifetime { get; set; }
		public string DataMimeType { get; set; }
		public string MetadataMimeType { get; set; }

		public int InitialRequestSize { get; set; } = 10;

		public static readonly RSocketClientOptions Default = new RSocketClientOptions()
		{
			KeepAlive = TimeSpan.FromMinutes(1),
			Lifetime = TimeSpan.FromMinutes(3),
			DataMimeType = BINARYMIMETYPE,
			MetadataMimeType = BINARYMIMETYPE,
		};
	}
}