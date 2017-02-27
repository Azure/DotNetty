using System;
using System.Threading.Tasks;

namespace DotNetty.Rpc.Client
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Threading;

    public class NettyClientFactory
    {
        private static readonly ConcurrentDictionary<string, NettyClient> ServiceClientMap = new ConcurrentDictionary<string, NettyClient>();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> ServiceClientAsyncLockMap = new ConcurrentDictionary<string, SemaphoreSlim>();

        public static async Task<NettyClient> Get(string serverAddress)
        {
            NettyClient client;
            ServiceClientMap.TryGetValue(serverAddress, out client);
            if (client != null)
                return client;

            SemaphoreSlim slim = ServiceClientAsyncLockMap.GetOrAdd(serverAddress, new SemaphoreSlim(1, 1));
            await slim.WaitAsync();

            ServiceClientMap.TryGetValue(serverAddress, out client);
            if (client != null)
                return client;

            try
            {
                client = new NettyClient();
                string[] array = serverAddress.Split(':');
                string host = array[0];
                int port = Convert.ToInt32(array[1]);
                EndPoint remotePeer = new IPEndPoint(IPAddress.Parse(host).MapToIPv6(), port);
                await client.Connect(remotePeer);
                ServiceClientMap.TryAdd(serverAddress, client);
                return client;
            }
            finally
            {
                slim.Release();
            }
        }

    }
}
