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
                        sa.Send($"App�Ŀ��������������˻�ȡ������ʾ����Ļ�ϣ�������Щ������߳־û�����������㼫Ϊ��Ҫ��ԭ��ֻ�ǰ�AFNetwork���η�װ��һ�£�ʹ�õ��ñ�úܼ򵥣���û�����εĿ���һЩ���⡣{i:00000}");
                    }

                    Console.WriteLine("Press any key to terminate the client process...");
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }
    }
}
