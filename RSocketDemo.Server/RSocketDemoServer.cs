using RSocket;
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

namespace RSocketDemo
{
	internal class RSocketDemoServer : RSocketServer
	{
		string _setupData;
		string _setupMetadata;

		public RSocketDemoServer(IRSocketTransport transport, RSocketOptions options = default)
			: base(transport, options)
		{
			this.Responder = this.ForRequestResponse;

			this.Streamer = this.ForRequestStream;

			this.Channeler = this.ForReuqestChannel;
		}

		protected override void HandleSetup(RSocketProtocol.Setup message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			this._setupData = data.ConvertToString();
			this._setupMetadata = metadata.ConvertToString();

			Console.WriteLine($"setupData: {this._setupData}, setupMetadata: {this._setupMetadata}");
			
			if (message.HasResume)
			{
				throw new UnsupportedSetupException("Resume operations are not supported.");
			}

			if (message.CanLease)
			{
				throw new UnsupportedSetupException("Lease operations are not supported.");
			}
		}

		protected override void HandleRequestFireAndForget(RSocketProtocol.RequestFireAndForget message, ReadOnlySequence<byte> metadata, ReadOnlySequence<byte> data)
		{
			Console.WriteLine($"client.RequestFireAndForget: {data.ConvertToString()},{metadata.ConvertToString()}");
		}

		public async ValueTask<Payload> ForRequestResponse((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			Console.WriteLine($"client.RequestResponse: {request.Data.ConvertToString()},{request.Metadata.ConvertToString()}");

			if (request.Data.ConvertToString() == "error")
			{
				throw new Exception("This is a test error when executing RequestResponse.");
			}

			return new Payload(request.Data, request.Metadata);
		}

		public IObservable<Payload> ForRequestStream((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			Console.WriteLine($"client.RequestStream: {request.Data.ConvertToString()},{request.Metadata.ConvertToString()}");

			//Returns an object that supports backpressure.
			return new OutputPublisher(this, 10);
			return this.ToRequesterStream();
		}

		IObservable<Payload> ForReuqestChannel((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request, IPublisher<Payload> incoming)
		{
			ISubscription subscription = incoming.Subscribe(a =>
		   {
			   Console.WriteLine($"client message: {a.Data.ConvertToString()}");
		   }, error =>
		   {
			   Console.WriteLine($"onError: {error.Message}");
		   }, () =>
		   {
			   Console.WriteLine($"onCompleted");
		   });

			Console.WriteLine($"sending request(n) to client: {int.MaxValue}");
			subscription.Request(int.MaxValue);

			int errorTrigger = 0;
			if (!int.TryParse(request.Metadata.ConvertToString(), out errorTrigger))
			{
				errorTrigger = int.MaxValue;
			}

			//Returns an object that supports backpressure.
			return new OutputPublisher(this, 10, errorTrigger);

			return Observable.Range(1, 10).Select(a =>
		   {
			   return new Payload($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
		   }
			);
		}

		IObservable<Payload> ToRequesterStream()
		{
			IObserver<int> ob = null;
			var p = Observable.Create<int>(o =>
			{
				ob = o;
				Task.Run(() =>
				{
					for (int i = 0; i < 5; i++)
					{
						//Thread.Sleep(1000);
						Console.WriteLine($"generating server data: {i}");
						o.OnNext(i);
					}

					o.OnCompleted();
				});

				return () =>
				{
					Console.WriteLine("server resources disposed");
				};
			});

			return p.Select(a =>
			{
				return new Payload($"data-{a}".ToReadOnlySequence(), $"metadata-{a}".ToReadOnlySequence());
			}
			);
		}
	}
}
