using System;

namespace RSocket
{
	public class RequesterChannel : Channel
	{
		IPublisher<Payload> _outgoing;

		public RequesterChannel(RSocket socket, int channelId, IObservable<Payload> outgoing) : base(socket, channelId)
		{
			this._outgoing = Helpers.AsPublisher(outgoing);
		}

		protected override IPublisher<Payload> CreateOutgoing()
		{
			return this._outgoing;
		}

		public override void OnIncomingCanceled()
		{
			if (this.IncomingFinished && this.OutgoingFinished)
				return;

			this.FinishIncoming();
			this.FinishOutgoing();
			this.SendCancelFrame();
		}
	}
}
