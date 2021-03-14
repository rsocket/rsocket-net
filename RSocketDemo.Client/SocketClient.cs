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
	class SocketClient
	{
		public static void Start()
		{
			//设定服务器IP地址  
			IPAddress ip = IPAddress.Parse("127.0.0.1");
			Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				clientSocket.Connect(new IPEndPoint(ip, 8888)); //配置服务器IP与端口  
				Console.WriteLine("连接服务器成功");
			}
			catch
			{
				Console.WriteLine("连接服务器失败，请按回车键退出！");
				return;
			}

			string sendMessage = "你好";//发送到服务端的内容
			clientSocket.Send(Encoding.UTF8.GetBytes(sendMessage));//向服务器发送数据，需要发送中文则需要使用Encoding.UTF8.GetBytes()，否则会乱码
			Console.WriteLine("向服务器发送消息：" + sendMessage);

			//接受从服务器返回的信息
			string recvStr = "";
			byte[] recvBytes = new byte[1024];
			int bytes;
			bytes = clientSocket.Receive(recvBytes, recvBytes.Length, 0);    //从服务器端接受返回信息 
			recvStr += Encoding.UTF8.GetString(recvBytes, 0, bytes);
			Console.WriteLine("服务端发来消息：{0}", recvStr);    //回显服务器的返回信息
														  //关闭连接并释放资源
			clientSocket.Close();
			Console.ReadLine();
		}
	}
}
