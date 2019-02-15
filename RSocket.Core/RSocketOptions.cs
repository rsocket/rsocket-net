using System;

namespace RSocket
{
	public class RSocketOptions
	{
		public const int INITIALDEFAULT = int.MinValue;
		public const string BINARYMIMETYPE = "binary";

		public TimeSpan KeepAlive { get; set; }
		public TimeSpan Lifetime { get; set; }
		public string DataMimeType { get; set; }
		public string MetadataMimeType { get; set; }

		public int InitialRequestSize { get; set; } = 10;
		public int GetInitialRequestSize(int initial) => (initial <= INITIALDEFAULT) ? InitialRequestSize : initial;


		public static readonly RSocketOptions Default = new RSocketOptions()
		{
			KeepAlive = TimeSpan.FromMinutes(1),
			Lifetime = TimeSpan.FromMinutes(3),
			DataMimeType = BINARYMIMETYPE,
			MetadataMimeType = BINARYMIMETYPE,
		};
	}
}