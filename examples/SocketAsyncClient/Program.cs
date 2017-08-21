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
                short iterations = 5000;

                using (var sa = new SocketClient(host, port))
                {
                    sa.Connect();

                    for (int i = 0; i < iterations; i++)
                    {
                        sa.Send("Usage: SocketAsyncClient Usage: SocketAsyncClient Usage: SocketAsyncClient" + i);
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
