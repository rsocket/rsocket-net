using RSocket.Exceptions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static RSocket.RSocketProtocol;

namespace RSocket
{
	public abstract class FrameHandler : IFrameHandler
	{
		bool _disposed = false;
		int _initialOutputRequest = 0;

		TaskCompletionSource<bool> _waitIncomingCompleteHandler = new TaskCompletionSource<bool>();

		Task _outputTask;
		CancellationTokenSource _inputCts = new CancellationTokenSource();
		CancellationTokenSource _outputCts = new CancellationTokenSource();

		Subject<Payload> _remotePayloads = new Subject<Payload>();

		public FrameHandler(RSocket socket)
		{
			this.Socket = socket;
			this._inputCts.Token.Register(this.StopIncoming);
			this._outputCts.Token.Register(this.StopOutging);

			this.Incoming = new IncomingStream(this._remotePayloads, this, this._waitIncomingCompleteHandler);
		}
		public FrameHandler(RSocket socket, int streamId) : this(socket)
		{
			this.StreamId = streamId;
		}

		public FrameHandler(RSocket socket, int streamId, int initialOutputRequest) : this(socket, streamId)
		{
			this._initialOutputRequest = initialOutputRequest;
		}

		public RSocket Socket { get; set; }
		public int StreamId { get; set; }


		public IPublisher<Payload> Incoming { get; private set; }
		public abstract IObservable<Payload> Outgoing { get; }


		public CancellationTokenSource OutputCts { get { return this._outputCts; } }

		IObserver<Payload> InboundSubscriber { get { return this._remotePayloads; } }
		IObserver<Payload> OutboundSubscriber { get; set; }
		ISubscription OutboundSubscription { get; set; }

		void CancelInput()
		{
			if (this._disposed)
				return;

			if (!this._inputCts.IsCancellationRequested)
				this._inputCts.Cancel();
		}
		void CancelOutput()
		{
			if (this._disposed)
				return;

			if (!this._outputCts.IsCancellationRequested)
				this._outputCts.Cancel();
		}
		protected void StopIncoming()
		{
			this.InboundSubscriber?.OnCompleted();
		}
		protected void StopOutging()
		{
			//cancel sending payload.

			/*
			 * await foeach (var item in IObservable`.ToAsyncEnumerable())
			 * {
			 *      //do some thing.
			 * }
			 */

			this.OutboundSubscriber?.OnCompleted();   //make sure the loop completed
			this.OutboundSubscription?.Dispose();
		}

		protected virtual void OnSubscribeOutputStream(ISubscription subscription)
		{
			//trigger generate output data.
			if (this._initialOutputRequest > 0)
				subscription.Request(this._initialOutputRequest);
		}

		public virtual void HandlePayload(RSocketProtocol.Payload message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			var handler = this._remotePayloads;

			if (message.IsNext)
			{
				handler.OnNext(new Payload(data, metadata));
			}

			if (message.IsComplete)
			{
				handler.OnCompleted();
			}
		}

		public virtual void HandleRequestN(RSocketProtocol.RequestN message)
		{
			this.OutboundSubscription.Request(message.RequestNumber);
		}

		public virtual void HandleCancel(RSocketProtocol.Cancel message)
		{
#if DEBUG
			Console.WriteLine($"frameHandler.HandleCancel() {this.OutputCts.Token.IsCancellationRequested}");
#endif

			this.CancelOutput();
		}
		public virtual void HandleError(RSocketProtocol.Error message)
		{
			this.CancelOutput();
			this.InboundSubscriber?.OnError(message.MakeException());
			this.CancelInput();
		}

		//called by InboundSubscription.
		public void SendCancel()
		{
			if (!this._inputCts.IsCancellationRequested)
			{
				this.Socket.SendCancel(this.StreamId).Wait();
			}
		}
		//called by InboundSubscription.
		public void SendRequest(int n)
		{
			if (!this._inputCts.IsCancellationRequested)
			{
				this.Socket.SendRequestN(this.StreamId, n).Wait();
			}
		}

		protected virtual Task GetInputTask()
		{
			return this._waitIncomingCompleteHandler.Task;
		}
		protected virtual Task GetOutputTask()
		{
			return this._outputTask;
		}

		async Task GetAwaitInputTask()
		{
			try
			{
				await this.GetInputTask();
			}
			catch (Exception ex)
			{
				//how to do？
			}
		}
		async Task GetAwaitOutputTask()
		{
			try
			{
				await this.GetOutputTask();
			}
			catch (Exception ex)
			{
				this.InboundSubscriber?.OnError(ex);
				this.CancelInput();
				await this.Socket.SendError(ErrorCodes.Application_Error, this.StreamId, $"{ex.Message}\n{ex.StackTrace}");
			}
		}
		protected virtual async Task GetCreatedTask()
		{
			var outputTask = this.GetAwaitOutputTask();
			var inputTask = this.GetAwaitInputTask();
			await Task.WhenAll(outputTask, inputTask);
		}

		protected virtual void OnTaskCreating()
		{
			var sourcePayloads = this.Outgoing;

			var outgoing = sourcePayloads as IPublisher<Payload>;
			if (outgoing == null)
				outgoing = new OutgoingStream(sourcePayloads);

			var outputStream = Observable.Create<Payload>(observer =>
			{
				this.OutboundSubscriber = observer;
				this.OutboundSubscription = outgoing.Subscribe(observer);
				this.OnSubscribeOutputStream(this.OutboundSubscription);

				return () =>
				{
					this.OutboundSubscription.Dispose();
				};
			});

			var outputTask = Helpers.ForEach(outputStream.ToAsyncEnumerable(),
				action: async value =>
				{
					await this.Socket.SendPayload(value, this.StreamId, false, true);
				},
				final: async () =>
				{
					await this.Socket.SendPayload(default(Payload), this.StreamId, true, false);
				}, cancel: this._outputCts.Token);

			this._outputTask = outputTask;
		}
		protected virtual void OnTaskCreated()
		{

		}

		public virtual async Task ToTask()
		{
			this.OnTaskCreating();
			var task = this.GetCreatedTask();
			this.OnTaskCreated();
			await task.ConfigureAwait(false);
		}

		public void Dispose()
		{
			if (this._disposed)
				return;

			this.CancelInput();
			this.CancelOutput();

			this._inputCts.Dispose();
			this._outputCts.Dispose();

			this.Dispose(true);

			this._disposed = true;
		}

		protected virtual void Dispose(bool disposing)
		{

		}
	}
}
