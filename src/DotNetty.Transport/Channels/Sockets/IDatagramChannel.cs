// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public interface IDatagramChannel : IChannel
    {
        bool IsConnected();

        Task JoinGroup(IPEndPoint multicastAddress);

        Task JoinGroup(IPEndPoint multicastAddress, TaskCompletionSource promise);

        Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface);

        Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, TaskCompletionSource promise);

        Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source);

        Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, TaskCompletionSource promise);

        Task LeaveGroup(IPEndPoint multicastAddress);

        Task LeaveGroup(IPEndPoint multicastAddress, TaskCompletionSource promise);

        Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface);

        Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, TaskCompletionSource promise);

        Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source);

        Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, TaskCompletionSource promise);
    }
}