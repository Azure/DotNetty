using System;
using System.Threading.Tasks;

namespace Rpc.Client
{
    using DotNetty.Rpc.Client;
    using Rpc.Models;

    public class Program
    {
        public static void Main(string[] args)
        {
            Test().Wait();
            Console.ReadKey();
        }


        public static async Task Test()
        {
            string serverAddress = "127.0.0.1:9008";
            NettyClient client = await NettyClientFactory.Get(serverAddress);
            var query = new TestCityQuery
            {
                Id = 1
            };
            CityInfo result= await client.SendRequest(query);
            Console.WriteLine(result);
        }
    }
}
