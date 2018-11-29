using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace RSocket.Reactive
{
	//TODO This goes to its own assembly for dependency management.

	static public class RSocketClientExtensions
	{
		//static public RSocketClientReactive<TSerializer> UseReactive<TSerializer>(this RSocketClient from) where TSerializer : IRSocketSerializer, new()
		//{
		//	//TODO Can we inject the serializer here? Maybe as a factory<T>?

		//}
	}

	public class RSocketClientReactive<TSerializer> : RSocketClient where TSerializer : IRSocketSerializer, new()
	{
		public RSocketClientReactive(IRSocketTransport transport, RSocketClientOptions options = default) : base(transport, options) { }

		//TODO Need to really think about single/multiple here. OptionalFeaturesPattern?
		//TODO Probably should be Serializer<T> in case there's caching/overhead... Some frameworks may not cache reflection metadata.
		public async ValueTask<IObservable<T>> RequestStream<T>(byte[] data, int initial = -1) => await base.RequestStream(new ReactiveTransform<T>(), data, initial);
		public async ValueTask<IObservable<T>> RequestChannel<T>(byte[] data, int initial = -1) => await base.RequestChannel(new ReactiveTransform<T>(), data, initial);

		//TODO Cold Observable too!

		public class ReactiveTransform<T> : IRSocketTransform, IObservable<T>
		{
			IObserver<T> Observer = new ReplaySubject<T>();
			static readonly IRSocketSerializer Serializer = new TSerializer();

			//TODO Review with the assumption of other methods here for the various accumulation modes.
			void IRSocketTransform.Next(ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> data) => Observer.OnNext(Serializer.Deserialize<T>(data));
			void IRSocketTransform.Complete() => Observer.OnCompleted();

			public IDisposable Subscribe(IObserver<T> observer) => ((IObservable<T>)Observer).Subscribe(observer);
		}
	}
}
