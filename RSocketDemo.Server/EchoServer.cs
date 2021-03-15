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
			this.Responder = this.ForRequestResponse;

			this.Streamer = this.ForRequestStream;

			//this.Channeler = this.ForReuqestChannel;
			this.Channeler = this.ForReuqestChannel1;
		}

		public override void HandleRequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Console.WriteLine($"收到客户端信息 RequestFireAndForget -{data.ConvertToString()},{metadata.ConvertToString()}");
		}

		public async ValueTask<PayloadContent> ForRequestResponse((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			Console.WriteLine($"收到客户端信息 RequestResponse -{request.Data.ConvertToString()},{request.Metadata.ConvertToString()}");
			return new PayloadContent(request.Data, request.Metadata);
		}

		public IObservable<PayloadContent> ForRequestStream((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			Console.WriteLine($"收到客户端信息 RequestStream -{request.Data.ToString()},{request.Metadata.ToString()}");
			return this.ToRequesterStream();
		}



		IAsyncEnumerable<PayloadContent> ForReuqestChannel((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request, IObservable<PayloadContent> incoming, ISubscription subscription)
		{
			subscription.Request(int.MaxValue);
			return incoming.ToAsyncEnumerable().Select(_ =>
			{
				string d = Encoding.UTF8.GetString(_.Data.ToArray());
				Console.WriteLine($"收到客户端信息-{d}");

				return _;
			});
		}

		IObservable<PayloadContent> ForReuqestChannel1((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request, IPublisher<PayloadContent> incoming)
		{
			ISubscription subscription = incoming.Subscribe(a =>
		   {
			   Console.WriteLine($"收到客户端信息-{a.Data.ConvertToString()}-{Thread.CurrentThread.ManagedThreadId}");
		   }, error =>
		   {
			   Console.WriteLine($"服务端onError");
		   }, () =>
		   {
			   Console.WriteLine($"服务端onCompleted");
		   });

			Console.WriteLine($"服务端向客户端Request-{int.MaxValue}");
			subscription.Request(int.MaxValue);

			return this.ToRequesterStream();

			return Observable.Range(1, 10).Select(a =>
		   {
			   return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
		   }
			);
		}

		IObservable<PayloadContent> ToRequesterStream()
		{
			IObserver<int> ob = null;
			var p = Observable.Create<int>(o =>
			{
				ob = o;
				Task.Run(() =>
				{
					for (int i = 0; i < 10; i++)
					{
						Thread.Sleep(1000);
						Console.WriteLine($"生成服务端消息-{i}");
						o.OnNext(i);
					}

					o.OnCompleted();
				});

				return () =>
				{
					Console.WriteLine("服务端stream dispose");
				};
			});

			return p.Select(a =>
			{
				return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);
		}
	}
}
