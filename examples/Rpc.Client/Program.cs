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
    using DotNetty.Handlers;

    public class Program
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance("Program");

        public static void Main(string[] args)
        {
            InternalLoggerFactory.DefaultFactory.AddProvider(new ConsoleLoggerProvider((s, level) => true, false));

            try
            {
                while (true)
                {
                    Test();
                    Console.ReadLine();
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
            string serverAddress = "192.168.139.132:9008";

            var sw = new Stopwatch();
            sw.Start();
            int count = 10000;
            var cde = new CountdownEvent(count);
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
                    if (n.IsFaulted)
                    {
                        if (n.Exception != null)
                        {
                            var exception = n.Exception.InnerException as TimeoutException;
                            if (exception == null)
                            {
                                Logger.Error(n.Exception.InnerException);
                            }
                        }
                    }
                    cde.Signal();
                });
            }

            cde.Wait();
            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
}
