using RSocket;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RSocketDemo
{
	public class RequestStreamSubscriber : SubscriberBase<PayloadContent>
	{
		TaskCompletionSource<bool> _incomingTaskSignal = new TaskCompletionSource<bool>();
		public List<string> MsgList { get; set; } = new List<string>();

		public int MaxReceives { get; set; } = int.MaxValue;

		public RequestStreamSubscriber(int requestSize) : base(requestSize)
		{
		}

		public override void DoOnNext(PayloadContent value)
		{
			string data = Encoding.UTF8.GetString(value.Data.ToArray());
			Console.WriteLine($"received message: {data}");
			this.MsgList.Add(data);

			if (this.MsgList.Count >= MaxReceives)
			{
				this.Subscription.Dispose();
				this.SetCompleted();
			}
		}

		public override void OnCompleted()
		{
			base.OnCompleted();
			this.SetCompleted();
		}

		void SetCompleted()
		{
			this._incomingTaskSignal.TrySetResult(true);
		}

		public void OnSubscribe(ISubscription subscription)
		{
			this.Subscription = subscription;
		}

		public async Task Block()
		{
			await this._incomingTaskSignal.Task;
		}
	}
}
