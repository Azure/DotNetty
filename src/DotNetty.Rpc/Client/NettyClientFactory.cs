namespace DotNetty.Rpc.Client
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;

    public class NettyClientFactory
    {
        private static readonly ConcurrentDictionary<string, Lazy<NettyClient>> ServiceClientMap = new ConcurrentDictionary<string, Lazy<NettyClient>>();

        public static NettyClient Get(string serverAddress)
        {
            Lazy<NettyClient> oldlazyClient;
            if (ServiceClientMap.TryGetValue(serverAddress, out oldlazyClient))
            {
                NettyClient client = oldlazyClient.Value;
                if (!client.IsClosed)
                {
                    return client;
                }
            }

            var newlazyClient = new Lazy<NettyClient>(() =>
            {
                var nettyClient = new NettyClient();
                string[] array = serverAddress.Split(':');
                string host = array[0];
                int port = Convert.ToInt32(array[1]);
                EndPoint remotePeer = new IPEndPoint(IPAddress.Parse(host).MapToIPv6(), port);
                nettyClient.Connect(remotePeer);
                return nettyClient;
            });

            if (oldlazyClient == null)
            {
                ServiceClientMap.TryAdd(serverAddress, newlazyClient);
            }
            else
            {
                ServiceClientMap.TryUpdate(serverAddress, newlazyClient, oldlazyClient);
            }
            return newlazyClient.Value;
        }

        public static bool Exist(string serverAddress) => ServiceClientMap.ContainsKey(serverAddress);
    }
}
