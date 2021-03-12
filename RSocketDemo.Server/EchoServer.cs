using RSocket;
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

namespace RSocketDemo
{
	internal class EchoServer : RSocketServer
	{
		public string ConnectionId { get; set; }
		public EchoServer(IRSocketTransport transport, RSocketOptions options = default, int echoes = 2)
			: base(transport, options)
		{


			// Request/Response
			Respond(
				request => request,                                 // requestTransform
				request =>
				{
					Console.WriteLine("收到客户端Respond信息");
					return AsyncEnumerable.Repeat(request, echoes);
				}, // producer
				result => result                                    // resultTransform
			);

			// Request/Stream
			Stream(
				request => request,                                 // requestTransform
				request =>
				{
					Console.WriteLine("收到客户端Stream信息");

					return AsyncEnumerable.Range(1, 20).Select(a => ($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence()));

					//return AsyncEnumerable.Repeat(request, 20);
				}, // producer
				result => result                                    // resultTransform
			);


			//this.Channeler = this.ForReuqestChannel;
			this.Channeler = this.ForReuqestChannel1;

			this.Channeler1 = ForReuqestChannel11;
		}

		IAsyncEnumerable<PayloadContent> ForReuqestChannel(PayloadContent request, IObservable<PayloadContent> incoming, ISubscription subscription)
		{
			subscription.Request(int.MaxValue);
			return incoming.ToAsyncEnumerable().Select(_ =>
			{
				string d = Encoding.UTF8.GetString(_.Data.ToArray());
				Console.WriteLine($"收到客户端信息-{d}");

				return _;
			});
		}

		IAsyncEnumerable<PayloadContent> ForReuqestChannel1(PayloadContent request, IObservable<PayloadContent> incoming, ISubscription subscription)
		{
			subscription.Request(int.MaxValue);

			incoming.ToAsyncEnumerable().Select(_ =>
		  {
			  //string d = Encoding.UTF8.GetString(_.data.ToArray());
			  //Console.WriteLine($"收到客户端信息-{d}");

			  return _;
		  }).ForEachAsync(a =>
		  {
			  Console.WriteLine($"收到客户端信息-{a.Data.ConvertToString()}-{Thread.CurrentThread.ManagedThreadId}");
			  Thread.Sleep(1000);
		  });

			return Observable.Range(1, 20).ToAsyncEnumerable().Select(a =>
			{
				Thread.Sleep(1000);
				Console.WriteLine($"发送服务端-{Thread.CurrentThread.ManagedThreadId}");
				return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);
		}

		IObservable<PayloadContent> ForReuqestChannel11(PayloadContent request, IObservable<PayloadContent> incoming)
		{
			incoming.Subscribe(a =>
			{
				Console.WriteLine($"收到客户端信息-{a.Data.ConvertToString()}-{Thread.CurrentThread.ManagedThreadId}");
			}, () =>
			{
				Console.WriteLine($"服务端onCompleted");
			}, error =>
			{
				Console.WriteLine($"服务端onError");
			},
			subscription =>
			{
				subscription.Request(int.MaxValue);
			});


			IObserver<int> ob = null;
			var p = Observable.Create<int>(o =>
			{
				ob = o;
				Task.Run(() =>
				{
					for (int i = 0; i < 10; i++)
					{
						//Thread.Sleep();
						o.OnNext(i);
					}

					o.OnCompleted();
				});

				return () =>
				{
					Console.WriteLine("服务端 dispose");
				};
			});

			return p.Select(a =>
			{
				Console.WriteLine($"生成服务端消息-{Thread.CurrentThread.ManagedThreadId}");
				return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);

			return Observable.Range(1, 10).Select(a =>
		   {
			   Console.WriteLine($"生成服务端消息-{Thread.CurrentThread.ManagedThreadId}");
			   return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
		   }
			);
		}
	}
}
