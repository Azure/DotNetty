// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests
{
    using System.Net;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    public class DatagramPacketDecoderTest
    {
        [Fact]
        public void Decode()
        {
            var channel = new EmbeddedChannel(new DatagramPacketDecoder(new StringDecoder(Encoding.UTF8)));
            var recipient = new IPEndPoint(IPAddress.Loopback, 10000);
            var sender = new IPEndPoint(IPAddress.Loopback, 20000);

            const string Content = "netty";
            IByteBuffer data = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(Content));
            try
            {
                Assert.True(channel.WriteInbound(new DatagramPacket(data, sender, recipient)));
                string content = channel.ReadInbound<string>();
                Assert.Equal(Content, content);
            }
            finally
            {
                Assert.False(channel.Finish());
            }
        }
    }
}
