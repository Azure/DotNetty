// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("UDP Transport Tests")]
    public class SocketDatagramChannelMulticastTest : TestBase
    {
        const int DefaultTimeOutInMilliseconds = 800;

        public SocketDatagramChannelMulticastTest(ITestOutputHelper output)
            : base(output)
        {
        }

        class MulticastTestHandler : SimpleChannelInboundHandler<DatagramPacket>
        {
            readonly ManualResetEventSlim resetEvent;
            bool done;
            bool fail;

            public MulticastTestHandler()
            {
                this.resetEvent = new ManualResetEventSlim(false);
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, DatagramPacket msg)
            {
                if (this.done)
                {
                    this.fail = true;
                }
                try
                {
                    this.done = msg.Content.ReadInt() == 1;
                }
                finally
                {
                    this.resetEvent.Set();
                }
            }

            public bool WaitForResult()
            {
                bool result = false;

                try
                {
                    if (this.resetEvent.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds)))
                    {
                        result = this.done && !this.fail;
                    }
                }
                finally
                {
                    this.resetEvent.Reset();
                    this.done = false;
                    this.fail = false;
                }

                return result;
            }
        }

        public static IEnumerable<object[]> GetData()
        {
            foreach (AddressFamily addressFamily in NetUtil.AddressFamilyTypes)
            {
                foreach (IByteBufferAllocator allocator in NetUtil.Allocators)
                {
                    yield return new object[]
                    {
                        addressFamily,
                        allocator
                    };
                }
            }
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void Multicast(AddressFamily addressFamily, IByteBufferAllocator allocator)
        {
            SocketDatagramChannel serverChannel = null;
            IChannel clientChannel = null;
            var serverGroup = new MultithreadEventLoopGroup(1);
            var clientGroup = new MultithreadEventLoopGroup(1);
            NetworkInterface loopback = NetUtil.LoopbackInterface(addressFamily);

            try
            {
                var multicastHandler = new MulticastTestHandler();
                var serverBootstrap = new Bootstrap();
                serverBootstrap
                    .Group(serverGroup)
                    .ChannelFactory(() => new SocketDatagramChannel(addressFamily))
                    .Option(ChannelOption.Allocator, allocator)
                    .Option(ChannelOption.SoReuseaddr, true)
                    .Option(ChannelOption.IpMulticastLoopDisabled, false)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        channel.Pipeline.AddLast(nameof(SocketDatagramChannelMulticastTest), multicastHandler);
                    }));

                IPAddress address = addressFamily == AddressFamily.InterNetwork 
                    ? IPAddress.Loopback : IPAddress.IPv6Loopback;

                this.Output.WriteLine($"Multicast server binding to:({addressFamily}){address}");
                Task<IChannel> task = serverBootstrap.BindAsync(address, IPEndPoint.MinPort);
                Assert.True(task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                    $"Multicast server binding to:({addressFamily}){address} timed out!");

                serverChannel = (SocketDatagramChannel)task.Result;
                var serverEndPoint = (IPEndPoint)serverChannel.LocalAddress;

                var clientBootstrap = new Bootstrap();
                clientBootstrap
                    .Group(clientGroup)
                    .ChannelFactory(() => new SocketDatagramChannel(addressFamily))
                    .Option(ChannelOption.Allocator, allocator)
                    .Option(ChannelOption.SoReuseaddr, true)
                    .Option(ChannelOption.IpMulticastLoopDisabled, false)
                    .Handler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        channel.Pipeline.AddLast("Dummy", new NetUtil.DummyHandler());
                    }));

                this.Output.WriteLine($"Multicast client binding to:({addressFamily}){address}");

                task = clientBootstrap.BindAsync(address, IPEndPoint.MinPort);
                Assert.True(task.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                    $"Multicast client binding to:({addressFamily}){address} timed out!");

                clientChannel = (SocketDatagramChannel)task.Result;

                IPAddress multicastAddress = addressFamily == AddressFamily.InterNetwork 
                    ? IPAddress.Parse("230.0.0.1") : IPAddress.Parse("ff12::1");
                var groupAddress = new IPEndPoint(multicastAddress, serverEndPoint.Port);
                Task joinTask = serverChannel.JoinGroup(groupAddress, loopback);
                Assert.True(joinTask.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                    $"Multicast server join group {groupAddress} timed out!");

                clientChannel.WriteAndFlushAsync(new DatagramPacket(Unpooled.Buffer().WriteInt(1), groupAddress)).Wait();
                Assert.True(multicastHandler.WaitForResult(), "Multicast server should have receivied the message.");

                Task leaveTask = serverChannel.LeaveGroup(groupAddress, loopback);
                Assert.True(leaveTask.Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds * 5)),
                    $"Multicast server leave group {groupAddress} timed out!");

                // sleep half a second to make sure we left the group
                Task.Delay(DefaultTimeOutInMilliseconds).Wait();

                // we should not receive a message anymore as we left the group before
                clientChannel.WriteAndFlushAsync(new DatagramPacket(Unpooled.Buffer().WriteInt(1), groupAddress)).Wait();
                Assert.False(multicastHandler.WaitForResult(), "Multicast server should not receive the message.");
            }
            finally
            {
                serverChannel?.CloseAsync().Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds));
                clientChannel?.CloseAsync().Wait(TimeSpan.FromMilliseconds(DefaultTimeOutInMilliseconds));

                Task.WaitAll(
                    serverGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                    clientGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
            }
        }
    }
}
