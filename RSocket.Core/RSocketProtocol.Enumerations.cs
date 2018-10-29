using System;
using System.Collections.Generic;
using System.Text;

namespace RSocket
{
	partial class RSocketProtocol
	{
		public const UInt16 MAJOR_VERSION = 1;
		public const UInt16 MINOR_VERSION = 0;

		public enum Types
		{
			/// <summary>Reserved</summary>
			Reserved = 0x00,
			/// <summary>Setup: Sent by client to initiate protocol processing.</summary>
			Setup = 0x01,
			/// <summary>Lease: Sent by Responder to grant the ability to send requests.</summary>
			Lease = 0x02,
			/// <summary>Keepalive: Connection keepalive.</summary>
			KeepAlive = 0x03,
			/// <summary>Request Response: Request single response.</summary>
			Request_Response = 0x04,
			/// <summary>Fire And Forget: A single one-way message.</summary>
			Request_Fire_And_Forget = 0x05,
			/// <summary>Request Stream: Request a completable stream.</summary>
			Request_Stream = 0x06,
			/// <summary>Request Channel: Request a completable stream in both directions.</summary>
			Request_Channel = 0x07,
			/// <summary>Request N: Request N more items with Reactive Streams semantics.</summary>
			Request_N = 0x08,
			/// <summary>Cancel Request: Cancel outstanding request.</summary>
			Cancel = 0x09,
			/// <summary>Payload: Payload on a stream. For example, response to a request, or message on a channel.</summary>
			Payload = 0x0A,
			/// <summary>Error: Error at connection or application level.</summary>
			Error = 0x0B,
			/// <summary>Metadata: Asynchronous Metadata frame</summary>
			Metadata_Push = 0x0C,
			/// <summary>Resume: Replaces SETUP for Resuming Operation (optional)</summary>
			Resume = 0x0D,
			/// <summary>Resume OK : Sent in response to a RESUME if resuming operation possible (optional)</summary>
			Resume_OK = 0x0E,
			/// <summary>Extension Header: Used To Extend more frame types as well as extensions.</summary>
			Extension = 0x3F,
		}

		public enum ErrorCodes : uint
		{
			/// <summary>Reserved</summary>
			Reserved = 0x00000000,
			/// <summary>The Setup frame is invalid for the server (it could be that the client is too recent for the old server). Stream ID MUST be 0.</summary>
			Invalid_Setup = 0x00000001,
			/// <summary>Some (or all) of the parameters specified by the client are unsupported by the server. Stream ID MUST be 0.</summary>
			Unsupported_Setup = 0x00000002,
			/// <summary>The server rejected the setup, it can specify the reason in the payload. Stream ID MUST be 0.</summary>
			Rejected_Setup = 0x00000003,
			/// <summary>The server rejected the resume, it can specify the reason in the payload. Stream ID MUST be 0.</summary>
			Rejected_Resume = 0x00000004,
			/// <summary>The connection is being terminated. Stream ID MUST be 0. Sender or Receiver of this frame MAY close the connection immediately without waiting for outstanding streams to terminate.</summary>
			Connection_Error = 0x00000101,
			/// <summary>The connection is being terminated. Stream ID MUST be 0. Sender or Receiver of this frame MUST wait for outstanding streams to terminate before closing the connection. New requests MAY not be accepted.</summary>
			Connection_Close = 0x00000102,
			/// <summary>Application layer logic generating a Reactive Streams onError event. Stream ID MUST be > 0.</summary>
			Application_Error = 0x00000201,
			/// <summary>Despite being a valid request, the Responder decided to reject it. The Responder guarantees that it didn't process the request. The reason for the rejection is explained in the Error Data section. Stream ID MUST be > 0.</summary>
			Rejected = 0x00000202,
			/// <summary>The Responder canceled the request but may have started processing it (similar to REJECTED but doesn't guarantee lack of side-effects). Stream ID MUST be > 0.</summary>
			Canceled = 0x00000203,
			/// <summary>The request is invalid. Stream ID MUST be > 0.</summary>
			Invalid = 0x00000204,
			/// <summary>Reserved for Extension Use</summary>
			Extension = 0xFFFFFFFF,
		}
	}
}
