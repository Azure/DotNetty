using System;
using System.Net;

namespace SocketAsyncServer
{
    public static class Program
    {
        public static void Main(String[] args)
        {
            try
            {
                int port = 9900;
                SocketListener sl = new SocketListener();
                sl.Start(port);

                Console.WriteLine("Server listening on port {0}. Press any key to terminate the server process...", port);
                Console.Read();

                sl.Stop();

            }
            catch (IndexOutOfRangeException)
            {
                PrintUsage();
            }
            catch (FormatException)
            {
                PrintUsage();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: SocketAsyncServer <port> [numConnections] [bufferSize].");
            Console.WriteLine("\t<port> Numeric value for the listening TCP port.");
            Console.WriteLine("\t[numConnections] Numeric value for the maximum number of incoming connections.");
            Console.WriteLine("\t[bufferSize] Numeric value for the buffer size of incoming connections.");
        }
    }
}
