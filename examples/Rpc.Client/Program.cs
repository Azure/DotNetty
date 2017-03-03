using System;
using System.Threading.Tasks;

namespace Rpc.Client
{
    using System.Diagnostics;
    using System.Threading;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Rpc.Client;
    using Microsoft.Extensions.Logging.Console;
    using Newtonsoft.Json;
    using Rpc.Models;
    using Rpc.Models.Test;

    public class Program
    {
        public static void Main(string[] args)
        {
            InternalLoggerFactory.DefaultFactory.AddProvider(new ConsoleLoggerProvider((s, level) => true, false));

            try
            {
                while (true)
                {
                    Test();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.ReadKey();
        }


        public static void Test()
        {
            string serverAddress = "192.168.19.39:9008";

            var sw = new Stopwatch();
            sw.Start();
            int count = 10000;
            var cde = new CountdownEvent(count);
            Parallel.For(0, count, (i) =>
            {
                NettyClient client = NettyClientFactory.Get(serverAddress);
                var query = new TestCityQuery
                {
                    Id = i
                };
                Task<CityInfo> task = client.SendRequest(query);
                task.ContinueWith(n =>
                {
                    cde.Signal();
                });
            });

            cde.Wait();
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}
