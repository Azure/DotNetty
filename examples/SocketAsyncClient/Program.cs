using System;

namespace SocketAsyncClient
{
    public static class Program
    {
        static void Main(string[] args)
        {
            try
            {
                String host = "10.1.4.204";
                Int32 port = 9900;
                Int16 iterations = 5000;

                using (SocketClient sa = new SocketClient(host, port))
                {
                    sa.Connect();

                    for (Int32 i = 0; i < iterations; i++)
                    {
                        sa.Send("Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]Usage: SocketAsyncClient <host> <port> [iterations]" + i);
                    }

                    Console.WriteLine("Press any key to terminate the client process...");
                    Console.ReadLine();
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                Console.WriteLine($"Usage: SocketAsyncClient <host> <port> [iterations] {ex.Message}");
            }
            catch (FormatException)
            {
                Console.WriteLine("Usage: SocketAsyncClient <host> <port> [iterations]." +
                    "\r\n\t<host> Name of the host to connect." +
                    "\r\n\t<port> Numeric value for the host listening TCP port." +
                    "\r\n\t[iterations] Number of iterations to the host.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }
    }
}
