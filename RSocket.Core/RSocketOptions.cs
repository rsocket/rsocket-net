using System;

namespace RSocket
{

	public class PrefetchOptions {
		public int InitialRequestSize { get; set; } = 8;

		public int GetInitialRequestSize(int initial) => initial <= 0 ? InitialRequestSize : initial;

		public static readonly PrefetchOptions Default = new PrefetchOptions();
	}

	public class RSocketOptions : PrefetchOptions
	{
		public const int INITIALDEFAULT = int.MinValue;
		public const string BINARYMIMETYPE = "application/octet-stream";

		public TimeSpan KeepAlive { get; set; }
		public TimeSpan Lifetime { get; set; }
		public string DataMimeType { get; set; }
		public string MetadataMimeType { get; set; }

		public static readonly RSocketOptions Default = new RSocketOptions()
		{
			KeepAlive = TimeSpan.FromMinutes(1),
			Lifetime = TimeSpan.FromMinutes(3),
			DataMimeType = BINARYMIMETYPE,
			MetadataMimeType = BINARYMIMETYPE,
		};
	}
}
