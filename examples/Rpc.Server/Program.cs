using System;

namespace Rpc.Server
{
    using DotNetty.Rpc.Server;

    public class Program
    {
        public static void Main(string[] args)
        {
            string serverAddress = "127.0.0.1:9008";
            var server = new RpcServer(serverAddress, n =>
            {
                return null;
            });
            server.StartAsync().Wait();
            Console.ReadKey();
        }
    }
}
