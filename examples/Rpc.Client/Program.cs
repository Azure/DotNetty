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
                    Console.ReadKey();
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
            string serverAddress = "127.0.0.1:9008";
            var sw = new Stopwatch();
            sw.Start();
            int k = 0;
            int count = 10000;
            for (int i = 0; i < count; i++)
            {
                NettyClient client = NettyClientFactory.Get(serverAddress);
                var query = new TestCityQuery
                {
                    Id = i
                };
                Task<CityInfo> task = client.SendRequest(query);
                task.ContinueWith(n =>
                {
                    //Console.WriteLine(JsonConvert.SerializeObject(n.Result));
                    Interlocked.Increment(ref k);
                });
            }

            while (true)
            {
                if (k == count)
                    break;
            }
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}
