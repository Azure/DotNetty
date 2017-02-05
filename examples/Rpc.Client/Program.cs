using System;
using System.Threading.Tasks;

namespace Rpc.Client
{
    using System.Diagnostics;
    using System.Threading;
    using DotNetty.Rpc.Client;
    using Rpc.Models;

    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Test();
                Console.ReadKey();
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
            NettyClient client = NettyClientFactory.Get(serverAddress).Result;

            var sw = new Stopwatch();
            sw.Start();
            int k = 0;
            int count = 10000;
            for (int i = 0; i < count; i++)
            {
                var query = new TestCityQuery
                {
                    Id = i
                };
                Task<CityInfo> task = client.SendRequest(query);
                task.ContinueWith(n =>
                {
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
