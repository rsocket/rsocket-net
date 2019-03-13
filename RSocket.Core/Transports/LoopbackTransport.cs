using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket.Transports
{
	public class LoopbackTransport : IRSocketTransport, IRSocketServerTransport
	{
		IDuplexPipe Front, Back;
		public PipeReader Input => Front.Input;
		public PipeWriter Output => Front.Output;
		//public IRSocketServerTransport Server => this;
		PipeReader IRSocketServerTransport.Input => Back.Input;
		PipeWriter IRSocketServerTransport.Output => Back.Output;

		public LoopbackTransport(PipeOptions inputoptions = default, PipeOptions outputoptions = default)
		{
			(Back, Front) = DuplexPipe.CreatePair(inputoptions, outputoptions);
		}

		//public Task ConnectAsync(CancellationToken cancel = default) => Task.CompletedTask;   //This is a noop because they are already connected.

		public Task StartAsync(CancellationToken cancel = default) => Task.CompletedTask;   //This is a noop because they are already connected.
		public Task StopAsync() => Task.CompletedTask;

		public IRSocketTransport Beyond => new ServerTransport(this); //TODO Maybe not Server? Backside? Otherside?

		struct ServerTransport : IRSocketTransport
		{
			IRSocketServerTransport Transport;
			public ServerTransport(IRSocketServerTransport transport) { Transport = transport; }
			public PipeReader Input => Transport.Input;
			public PipeWriter Output => Transport.Output;
			public Task StartAsync(CancellationToken cancel = default) => Transport.StartAsync(cancel);
			public Task StopAsync() => Transport.StopAsync();
		}
	}
}
