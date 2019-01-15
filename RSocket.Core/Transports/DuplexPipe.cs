using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace RSocket.Transports
{
	//IDuplexPipe is defined in System.IO.Pipelines, but they provide no default implementation!!! So something like the below occurs all over the place: SignalR, etc. Also, Input and Output are awful names, but I agree they're hard to name.

	//              BACK             //
	//        output    input        //
	//        ---------------        //
	//        writer   reader        //
	//          ||       /\	         //
	//          ||       ||          //
	//          ||       ||          //
	//          ||       ||          //
	//          \/       ||          //
	//        reader   writer        //
	//        ---------------        //
	//        input    output        //
	//             FRONT	         //

	public class DuplexPipe : IDuplexPipe
	{
		public PipeReader Input { get; }
		public PipeWriter Output { get; }

		public DuplexPipe(PipeWriter writer, PipeReader reader) { Input = reader; Output = writer; }

		public static (IDuplexPipe Front, IDuplexPipe Back) CreatePair(PipeOptions fronttobackOptions, PipeOptions backtofrontOptions)
		{
			Pipe FrontToBack = new Pipe(backtofrontOptions ?? DefaultOptions), BackToFront = new Pipe(fronttobackOptions ?? DefaultOptions);
			return (new DuplexPipe(FrontToBack.Writer, BackToFront.Reader), new DuplexPipe(BackToFront.Writer, FrontToBack.Reader));
		}

		public static readonly PipeOptions DefaultOptions = new PipeOptions(writerScheduler: PipeScheduler.ThreadPool, readerScheduler: PipeScheduler.ThreadPool, useSynchronizationContext: false, pauseWriterThreshold: 0, resumeWriterThreshold: 0);
		public static readonly PipeOptions ImmediateOptions = new PipeOptions(writerScheduler: PipeScheduler.Inline, readerScheduler: PipeScheduler.Inline, useSynchronizationContext: true, pauseWriterThreshold: 0, resumeWriterThreshold: 0);
	}
}
