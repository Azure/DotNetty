// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests
{
    using System.Net;
    using System.Text;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;

    public class DatagramPacketEncoderTest
    {
        [Fact]
        public void Encode()
        {
            var channel = new EmbeddedChannel(new DatagramPacketEncoder<string>(new StringEncoder(Encoding.UTF8)));
            var recipient = new IPEndPoint(IPAddress.Loopback, 10000);
            var sender = new IPEndPoint(IPAddress.Loopback, 20000);
            const string Content = "netty";
            Assert.True(channel.WriteOutbound(new DefaultAddressedEnvelope<string>(Content, sender, recipient)));

            var packet = channel.ReadOutbound<DatagramPacket>();
            Assert.NotNull(packet);
            try
            {
                Assert.Equal(Content, packet.Content.ToString(Encoding.UTF8));
                Assert.Equal(sender, packet.Sender);
                Assert.Equal(recipient, packet.Recipient);
            }
            finally
            {
                packet.Release();
                Assert.False(channel.Finish());
            }
        }

        [Fact]
        public void UnmatchedMessageType()
        {
            var channel = new EmbeddedChannel(new DatagramPacketEncoder<string>(new StringEncoder(Encoding.UTF8)));
            var recipient = new IPEndPoint(IPAddress.Loopback, 10000);
            var sender = new IPEndPoint(IPAddress.Loopback, 20000);

            var envelop = new DefaultAddressedEnvelope<long>(101L, sender, recipient);
            Assert.True(channel.WriteOutbound(envelop));

            var defaultEnvelop = channel.ReadOutbound<DefaultAddressedEnvelope<long>>();
            Assert.NotNull(defaultEnvelop);
            try
            {
                Assert.Same(envelop, defaultEnvelop);
            }
            finally
            {
                defaultEnvelop.Release();
                Assert.False(channel.Finish());
            }
        }

        [Fact]
        public void UnmatchedType()
        {
            var channel = new EmbeddedChannel(new DatagramPacketEncoder<string>(new StringEncoder(Encoding.UTF8)));

            try
            {
                const string Content = "netty";
                Assert.True(channel.WriteOutbound(Content));

                string content = channel.ReadOutbound<string>();
                Assert.Same(Content, content);
            }
            finally
            {
                Assert.False(channel.Finish());
            }
        }
    }
}
