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
			this.FireAndForgetHandler = this.ForRequestFireAndForget;
			this.Responder = this.ForRequestResponse;
			this.Streamer = this.ForRequestStream;
			this.Channeler = this.ForRequestChannel;
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

		public void ForRequestFireAndForget((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			Console.WriteLine($"client.RequestFireAndForget: {request.Data.ConvertToString()},{request.Metadata.ConvertToString()}");

			if (request.Metadata.ConvertToString() == "handle.request.error")
			{
				throw new Exception("This is a test error while handling RequestFireAndForget.");
			}
		}

		public async ValueTask<Payload> ForRequestResponse((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			await Task.CompletedTask;
			Console.WriteLine($"client.RequestResponse: {request.Data.ConvertToString()},{request.Metadata.ConvertToString()}");

			if (request.Metadata.ConvertToString() == "handle.request.error")
			{
				throw new Exception("This is a test error while handling RequestResponse.");
			}

			return new Payload(request.Data, request.Metadata);
		}

		public IObservable<Payload> ForRequestStream((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request)
		{
			string data = request.Data.ConvertToString();
			string metadata = request.Metadata.ConvertToString();

			Console.WriteLine($"client.RequestStream: {data},{metadata}");

			if (metadata == "handle.request.error")
			{
				throw new Exception("This is a test error while handling RequestStream.");
			}

			if (metadata == "gen.data.error")
			{
				int errorTrigger = 0;
				if (!int.TryParse(data, out errorTrigger))
				{
					errorTrigger = 5;
				}

				return new OutputPublisher(this, 10, errorTrigger);
			}

			//Returns an object that supports backpressure.
			return new OutputPublisher(this, 5);
		}

		IObservable<Payload> ForRequestChannel((ReadOnlySequence<byte> Data, ReadOnlySequence<byte> Metadata) request, IPublisher<Payload> incoming)
		{
			string data = request.Data.ConvertToString();
			string metadata = request.Metadata.ConvertToString();

			Console.WriteLine($"ForRequestChannel: {data},{metadata}");

			if (metadata == "handle.request.error")
			{
				throw new Exception("This is a test error while handling RequestChannel.");
			}

			if (metadata == "gen.data.error")
			{
				this.SubscribeIncoming(data, metadata, incoming);

				int errorTrigger = 0;
				if (!int.TryParse(data, out errorTrigger))
				{
					errorTrigger = 5;
				}

				return new OutputPublisher(this, 10, errorTrigger);
			}

			if (metadata == "echo")
			{
				var echoData = Observable.Create<Payload>(observer =>
				{
					var sub = incoming.Subscribe(observer);
					sub.Request(int.MaxValue);

					return Disposable.Empty;
				});

				return echoData;
			}

			this.SubscribeIncoming(data, metadata, incoming);

			//Returns an object that supports backpressure.
			return new OutputPublisher(this, 5);
		}

		void SubscribeIncoming(string data, string metadata, IPublisher<Payload> incoming)
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
		}
	}
}
