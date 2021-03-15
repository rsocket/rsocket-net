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

namespace RSocketDemo
{
	internal class EchoServer : RSocketServer
	{
		public string ConnectionId { get; set; }
		public EchoServer(IRSocketTransport transport, RSocketOptions options = default, int echoes = 2)
			: base(transport, options)
		{


			//// Request/Response
			//Respond(
			//	request => request,                                 // requestTransform
			//	request =>
			//	{
			//		Console.WriteLine("收到客户端Respond信息");
			//		return AsyncEnumerable.Repeat(request, echoes);
			//	}, // producer
			//	result => result                                    // resultTransform
			//);


			//this.Channeler = this.ForReuqestChannel;
			//this.Channeler = this.ForReuqestChannel1;
		}

		IAsyncEnumerable<(ReadOnlySequence<byte> data, ReadOnlySequence<byte> metadata)> ForReuqestChannel((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request, IObservable<(ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)> incoming, ISubscription subscription)
		{
			subscription.Request(int.MaxValue);
			return incoming.ToAsyncEnumerable().Select(_ =>
			{
				string d = Encoding.UTF8.GetString(_.data.ToArray());
				Console.WriteLine($"收到客户端信息-{d}");

				return (_.data, _.metadata);
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
			  Thread.Sleep(500);
		  });

			return Observable.Range(1, 10).ToAsyncEnumerable().Select(a =>
			{
				Thread.Sleep(500);
				//Console.WriteLine($"aaaa-{Thread.CurrentThread.ManagedThreadId}");
				return new PayloadContent($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);
		}
	}
}
