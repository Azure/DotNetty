

namespace Rpc.Server
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Extensions.Logging.Console;

    public class Program
    {
        public static void Main(string[] args)
        {
            Environment.SetEnvironmentVariable("io.netty.leakDetection.level", "Disabled");

            InternalLoggerFactory.DefaultFactory.AddProvider(new ConsoleLoggerProvider((s, level) => true, false));

            string serverAddress = "0.0.0.0:9008";
            var server = new DotNetty.Rpc.Server.RpcServer(serverAddress);
            server.StartAsync().Wait();
        }
    }
}
