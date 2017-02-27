using System;

namespace DotNetty.Rpc.Client
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Threading;

    public class NettyClientFactory
    {
        private static readonly ConcurrentDictionary<string, NettyClient> ServiceClientMap = new ConcurrentDictionary<string, NettyClient>();
        private static readonly ConcurrentDictionary<string, object> ServiceClientLockMap = new ConcurrentDictionary<string, object>();

        public static NettyClient Get(string serverAddress)
        {
            NettyClient client;
            ServiceClientMap.TryGetValue(serverAddress, out client);
            if (client != null && !client.IsClosed)
                return client;

            object locker = ServiceClientLockMap.GetOrAdd(serverAddress, new SemaphoreSlim(1, 1));
            lock (locker)
            {
                ServiceClientMap.TryGetValue(serverAddress, out client);
                if (client != null && !client.IsClosed)
                    return client;

                client = new NettyClient();
                string[] array = serverAddress.Split(':');
                string host = array[0];
                int port = Convert.ToInt32(array[1]);
                EndPoint remotePeer = new IPEndPoint(IPAddress.Parse(host).MapToIPv6(), port);
                client.Connect(remotePeer);

                ServiceClientMap.TryAdd(serverAddress, client);
                return client;
            }
        }

        public bool Exist(string serverAddress) => ServiceClientMap.ContainsKey(serverAddress);
    }
}
