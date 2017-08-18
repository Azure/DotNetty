

namespace Rpc.Server
{
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Extensions.Logging.Console;

    public class Program
    {
        public static void Main(string[] args)
        {
            InternalLoggerFactory.DefaultFactory.AddProvider(new ConsoleLoggerProvider((s, level) => true, false));

            string serverAddress = "0.0.0.0:9008";
            var server = new DotNetty.Rpc.Server.RpcServer(serverAddress);
            server.StartAsync().Wait();
        }
    }
}
