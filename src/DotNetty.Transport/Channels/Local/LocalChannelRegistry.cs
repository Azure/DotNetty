// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Transport.Channels.Local
{
    using System.Collections.Concurrent;
    using System.Net;

    public static class LocalChannelRegistry
    {
        static readonly ConcurrentDictionary<LocalAddress, IChannel> BoundChannels = new ConcurrentDictionary<LocalAddress, IChannel>();

        internal static LocalAddress Register(IChannel channel, LocalAddress oldLocalAddress, EndPoint localAddress) 
        {
            if (oldLocalAddress != null) 
            {
                throw new ChannelException("already bound");
            }
            
            if (!(localAddress is LocalAddress)) 
            {
                throw new ChannelException($"unsupported address type: {localAddress.GetType()}");
            }

            var addr = (LocalAddress) localAddress;
            if (LocalAddress.Any.Equals(addr)) 
            {
                addr = new LocalAddress(channel);
            }

            var result = BoundChannels.GetOrAdd(addr, channel);
            if (!ReferenceEquals(result, channel))
            {
                throw new ChannelException($"address already in use by: {result}");
            }
            
            return addr;
        }

        internal static IChannel Get(EndPoint localAddress) 
            => localAddress is LocalAddress key && BoundChannels.TryGetValue(key, out var ch) ? ch : null;

        internal static void Unregister(LocalAddress localAddress) 
            => BoundChannels.TryRemove(localAddress, out var _);
    }
}