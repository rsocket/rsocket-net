//// Copyright (c) .NET Foundation. All rights reserved.
//// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


// This file is meant to merge in all of the the little dependencies that the SignalR WebSocket implementation brings in that have low modification frequency.

//using System;


namespace Microsoft.AspNetCore.Connections
{
	[System.Flags]
	public enum TransferFormat
	{
		Binary = 1,
		Text = 2,
	}
}


//namespace Microsoft.AspNetCore.Http.Connections
//{
//	/// <summary>
//	/// Constants related to HTTP transports.
//	/// </summary>
//	public static class HttpTransports
//	{
//		/// <summary>
//		/// A bitmask comprised of all available <see cref="HttpTransportType"/> values.
//		/// </summary>
//		public static readonly HttpTransportType All = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
//	}
//}



//namespace Microsoft.AspNetCore.Http.Connections
//{
//	/// <summary>
//	/// Specifies transports that the client can use to send HTTP requests.
//	/// </summary>
//	/// <remarks>
//	/// This enumeration has a <see cref="FlagsAttribute"/> attribute that allows a bitwise combination of its member values.
//	/// </remarks>
//	[Flags]
//	public enum HttpTransportType
//	{
//		/// <summary>
//		/// Specifies that no transport is used.
//		/// </summary>
//		None = 0,
//		/// <summary>
//		/// Specifies that the web sockets transport is used.
//		/// </summary>
//		WebSockets = 1,
//		/// <summary>
//		/// Specifies that the server sent events transport is used.
//		/// </summary>
//		ServerSentEvents = 2,
//		/// <summary>
//		/// Specifies that the long polling transport is used.
//		/// </summary>
//		LongPolling = 4,
//	}
//}