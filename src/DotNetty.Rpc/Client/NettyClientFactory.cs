using System;
using System.Threading.Tasks;

namespace DotNetty.Rpc.Client
{
    using System.Collections.Concurrent;
    using System.Net;

    public class NettyClientFactory
    {
        private static readonly ConcurrentDictionary<string, NettyClient> ServiceClientMap = new ConcurrentDictionary<string, NettyClient>();

        public static async Task<NettyClient> Get(string serverAddress)
        {
            NettyClient client;
            ServiceClientMap.TryGetValue(serverAddress, out client);
            if (client != null)
                return client;

            var newClient = new NettyClient();
            string[] array = serverAddress.Split(':');
            string host = array[0];
            int port = Convert.ToInt32(array[1]);
            EndPoint remotePeer = new IPEndPoint(IPAddress.Parse(host).MapToIPv6(), port);
            await newClient.Connect(remotePeer);

            ServiceClientMap.TryAdd(serverAddress, newClient);
            return newClient;
        }

    }
}
