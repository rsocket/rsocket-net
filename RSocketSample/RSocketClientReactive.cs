using System;
using System.Reactive.Linq;
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
		//public async ValueTask<IObservable<T>> RequestStream<T>(byte[] data, int initial = -1) => await base.RequestStream(new ReactiveTransform<T>(), data, initial);
		//public async ValueTask<IObservable<T>> RequestChannel<T>(byte[] data, int initial = -1) => await base.RequestChannel(new ReactiveStream<T>(), data, initial);

		//public IObservable<T> RequestStream<T>(byte[] data, int initial = -1) => ReactiveStream<T>.Create(stream => base.RequestStream(stream, data, initial));
		public IObservable<T> RequestStream<T>(byte[] data, byte[] metadata = null, int initial = RSocketClient.INITIALDEFAULT) => Observable.Create<T>(observer => { var stream = new ReactiveStream<T>(observer); base.RequestStream(stream, data, metadata: metadata, initial: initial); return stream.Task; });

		//return Observable.Create<T>(async observer =>
		//{
		//	await base.RequestStream(new ReactiveTransform<T>(observer), data, initial);
		//	//TODO On Dispose, kill the stream here... //return () => ;
		//});


		//TODO Expose Serializer? As Factory?

		public class ReactiveStream<T> : IRSocketStream
		{
			readonly IObserver<T> Observer;
			readonly TaskCompletionSource<object> Completion = new TaskCompletionSource<object>();
			public Task Task => Completion.Task;

			public ReactiveStream(IObserver<T> observer) { Observer = observer; }

			void IRSocketStream.Next(ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> data) => Observer.OnNext(Serializer.Deserialize<T>(data));
			void IRSocketStream.Complete() { Observer.OnCompleted(); Completion.SetResult(null); }

			static readonly IRSocketSerializer Serializer = new TSerializer();
			static public IObservable<T> Create(Action<IRSocketStream> action) => Observable.Create<T>(observer => { var stream = new ReactiveStream<T>(observer); action(stream); return stream.Task; });
		}

		//public class ReactiveStream<T> : IRSocketStream, IObservable<T>
		//{
		//	IObserver<T> Observer = new ReplaySubject<T>();
		//	static readonly IRSocketSerializer Serializer = new TSerializer();

		//	public ReactiveStream() { Observer = new ReplaySubject<T>(); }
		//	public ReactiveStream(IObserver<T> observer) { Observer = observer; }

		//	void IRSocketStream.Next(ReadOnlySpan<byte> metadata, ReadOnlySpan<byte> data) => Observer.OnNext(Serializer.Deserialize<T>(data));
		//	void IRSocketStream.Complete() => Observer.OnCompleted();

		//	public IDisposable Subscribe(IObserver<T> observer) => ((IObservable<T>)Observer).Subscribe(observer);
		//}
	}
}
