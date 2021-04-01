using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RSocketDemo
{
	public static class LogHelper
	{
		static readonly object LockObject = new object();
		static string LogFileName { get; set; }
		static LogHelper()
		{
			LogFileName = $"runlog_{DateTime.Now.ToString("yyyyMMddHHmmss")}.txt";
		}

		public static void Log(string log)
		{
			lock (LockObject)
			{
				try
				{
					File.AppendAllText(LogFileName, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff")}： {log}\n");
				}
				catch
				{
				}
			}
		}

		public static void Log(Exception ex, string mark = "")
		{
			mark = string.IsNullOrEmpty(mark) ? "" : $"{mark} -->";
			string log = $"{mark}{ex.Message}\n{ex.StackTrace}";
			Log(log);
		}
	}
}
