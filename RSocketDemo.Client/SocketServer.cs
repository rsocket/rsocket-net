using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using RSocket;
using RSocket.Transports;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Buffers;
using System.Text;
using System.Reactive.Disposables;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Reactive.Subjects;

namespace RSocketDemo
{
	class SocketServer
	{
		//创建套接字  
		static Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		private static byte[] result = new byte[1024];

		public static void Start()
		{
			Task.Run(() =>
			{
				Console.WriteLine("服务端已启动");
				string host = "127.0.0.1";//IP地址
				int port = 8888;//端口
				socket.Bind(new IPEndPoint(IPAddress.Parse(host), port));
				socket.Listen(100);//设定最多100个排队连接请求   
				Thread myThread = new Thread(ListenClientConnect);//通过多线程监听客户端连接  
				myThread.Start();
				Console.ReadLine();
			});
		}

		/// <summary>  
		/// 监听客户端连接  
		/// </summary>  
		private static void ListenClientConnect()
		{
			while (true)
			{
				Socket clientSocket = socket.Accept();
				//clientSocket.Send(Encoding.UTF8.GetBytes("服务器连接成功"));
				Thread receiveThread = new Thread(ReceiveMessage);
				receiveThread.Start(clientSocket);
			}
		}

		/// <summary>  
		/// 接收消息  
		/// </summary>  
		/// <param name="clientSocket"></param>  
		private static void ReceiveMessage(object clientSocket)
		{
			Socket myClientSocket = (Socket)clientSocket;
			while (true)
			{
				try
				{
					//通过clientSocket接收数据  
					int receiveNumber = myClientSocket.Receive(result);
					if (receiveNumber == 0)
						return;
					Console.WriteLine("接收客户端{0} 的消息：{1}", myClientSocket.RemoteEndPoint.ToString(), Encoding.UTF8.GetString(result, 0, receiveNumber));
					//给Client端返回信息
					string sendStr = "已成功接到您发送的消息";
					byte[] bs = Encoding.UTF8.GetBytes(sendStr);//Encoding.UTF8.GetBytes()不然中文会乱码
					myClientSocket.Send(bs, bs.Length, 0);  //返回信息给客户端
					myClientSocket.Close(); //发送完数据关闭Socket并释放资源
					Console.ReadLine();
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					myClientSocket.Shutdown(SocketShutdown.Both);//禁止发送和上传
					myClientSocket.Close();//关闭Socket并释放资源
					break;
				}
			}
		}
	}
}
