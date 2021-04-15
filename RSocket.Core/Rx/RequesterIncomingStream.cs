using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocket
{
	class RequesterIncomingStream : IPublisher<Payload>, IObservable<Payload>
	{
		RSocket Socket;
		IObservable<Payload> _outputs;
		Func<int, Task> _channelEstablisher;

		public RequesterIncomingStream(RSocket socket, IObservable<Payload> outputs, Func<int, Task> channelEstablisher)
		{
			this.Socket = socket;
			this._outputs = outputs;
			this._channelEstablisher = channelEstablisher;
		}

		IDisposable IObservable<Payload>.Subscribe(IObserver<Payload> observer)
		{
			return (this as IPublisher<Payload>).Subscribe(observer);
		}

		ISubscription IPublisher<Payload>.Subscribe(IObserver<Payload> observer)
		{
			var channelId = this.Socket.NewStreamId();

			RequesterChannel channel = new RequesterChannel(this.Socket, channelId, this._outputs);

			this.Socket.AddChannel(channel);
			var sub = channel.Incoming.Subscribe(observer);

			this.Socket.Schedule(channelId, async (stream, cancel) =>
			{
				try
				{
					await this._channelEstablisher(channelId).ConfigureAwait(false);
					Task channelTask = channel.ToTask();
					this.OnSubscribe(channel);
					await channelTask;
				}
				finally
				{
					this.Socket.RemoveChannel(channelId);
					channel.Dispose();

#if DEBUG
					Console.WriteLine($"----------------Channel of requester has terminated: stream[{channelId}]----------------");
#endif
				}
			});

			return sub;
		}

		protected virtual void OnSubscribe(Channel channel)
		{

		}
	}

}
