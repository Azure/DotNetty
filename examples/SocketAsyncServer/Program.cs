using System;
using System.Net;

namespace SocketAsyncServer
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                int port = 9900;
                var sl = new SocketListener();
                sl.Start(port);

                Console.WriteLine("Server listening on port {0}. Press any key to terminate the server process...", port);
                Console.Read();

                sl.Stop();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
