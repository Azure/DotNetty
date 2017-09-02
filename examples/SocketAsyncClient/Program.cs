using System;

namespace SocketAsyncClient
{
    public static class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string host = "10.1.4.204";
                int port = 9900;
                short iterations = 2000;

                using (var sa = new SocketClient(host, port))
                {
                    sa.Connect();

                    for (int i = 0; i < iterations; i++)
                    {
                        sa.Send($"App的开发无外乎从网络端获取数据显示在屏幕上，数据做些缓存或者持久化，所以网络层极为重要。原来只是把AFNetwork二次封装了一下，使得调用变得很简单，并没有深层次的考虑一些问题。{i:00000}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.Message);
            }
        }
    }
}
