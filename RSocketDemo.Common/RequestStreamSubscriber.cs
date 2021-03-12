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
		public List<string> MsgList { get; set; } = new List<string>();

		public RequestStreamSubscriber(int requestSize) : base(requestSize)
		{
		}

		public override void DoOnNext(PayloadContent value)
		{
			string data = Encoding.UTF8.GetString(value.Data.ToArray());
			Console.WriteLine(data);
			this.MsgList.Add(data);

			if (this.MsgList.Count > 5)
				this.Subscription.Cancel();
		}

		public override void OnSubscribe(ISubscription subscription)
		{
			base.OnSubscribe(subscription);

			//Task.Run(() =>
			//{
			//	Thread.Sleep(5000);
			//	Console.WriteLine("this.Subscription.Cancel()");
			//	this.Subscription.Cancel();
			//});
		}
	}
}
