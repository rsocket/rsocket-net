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
			Console.WriteLine($"收到服务端消息-{data}");
			this.MsgList.Add(data);

			if (this.MsgList.Count >= MaxReceives)
				this.Subscription.Dispose();
		}

		public override void OnCompleted()
		{
			base.OnCompleted();
			this._incomingTaskSignal.TrySetResult(true);
		}

		public void OnSubscribe(ISubscription sub)
		{
			this.Subscription = sub;
		}

		public async Task Block()
		{
			await this._incomingTaskSignal.Task;
		}
	}
}
