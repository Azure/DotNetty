

namespace Rpc.Server
{
    using System;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Rpc.Server;
    using Microsoft.Extensions.Logging.Console;

    public class Program
    {
        public static void Main(string[] args)
        {
            InternalLoggerFactory.DefaultFactory.AddProvider(new ConsoleLoggerProvider((s, level) => true, false));

            string serverAddress = "127.0.0.1:9008";
            var server = new RpcServer(serverAddress);
            server.StartAsync().Wait();
            Console.ReadKey();
        }
    }
}
