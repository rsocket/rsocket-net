using RSocket.Channels;
using System;

namespace RSocket
{
	class IncomingStreamSubscriber : Subscriber<Payload>, IObserver<Payload>
	{
		IObserver<Payload> _observer;
		Channel _channel;

		public IncomingStreamSubscriber(IObserver<Payload> observer, Channel channel)
		{
			this._observer = observer;
			this._channel = channel;
		}

		protected override void DoOnCompleted()
		{
			try
			{
				this._observer.OnCompleted();
			}
			catch
			{
			}
		}

		protected override void DoOnError(Exception error)
		{
			try
			{
				this._observer.OnError(error);
			}
			catch
			{
			}
		}

		protected override void DoOnNext(Payload value)
		{
			try
			{
				this._observer.OnNext(value);
			}
			catch (Exception ex)
			{
#if DEBUG
				Console.WriteLine($"An exception occurred while incoming subscriber handling payload: {ex.Message}\n{ex.StackTrace}");
#endif

				this._channel.OnIncomingSubscriberOnNextError();
			}
		}
	}
}
