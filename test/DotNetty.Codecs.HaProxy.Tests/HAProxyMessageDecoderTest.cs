// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy.Tests
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;
    using static DotNetty.Codecs.HaProxy.HAProxyProxiedProtocol;

    public static class DecoderTestHelper
    {
        public static int Count(this IEnumerator<IChannelHandler> enumerator)
        {
            int count = enumerator.Current == null ? 0 : 1;
            while (enumerator.MoveNext())
            {
                count++;
            }
            return count;
        }
    }

    public class HAProxyMessageDecoderTest
    {

        EmbeddedChannel ch;

        public HAProxyMessageDecoderTest()
        {
            ch = new EmbeddedChannel(new HAProxyMessageDecoder());
        }

        private static IByteBuffer CopiedBuffer(string value, Encoding encoding)
        {
            return Unpooled.CopiedBuffer(value.ToCharArray(), 0, value.Length, encoding);
        }

        [Fact]
        public void TestIPV4Decode()
        {
            int startChannels = ch.Pipeline.GetEnumerator().Count();
            string header = "PROXY TCP4 192.168.0.1 192.168.0.11 56324 443\r\n";
            ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V1, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.TCP4, msg.ProxiedProtocol());
            Assert.Equal("192.168.0.1", msg.SourceAddress());
            Assert.Equal("192.168.0.11", msg.DestinationAddress());
            Assert.Equal(56324, msg.SourcePort());
            Assert.Equal(443, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestIPV6Decode()
        {
            int startChannels = ch.Pipeline.GetEnumerator().Count();
            string header = "PROXY TCP6 2001:0db8:85a3:0000:0000:8a2e:0370:7334 1050:0:0:0:5:600:300c:326b 56324 443\r\n";
            ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V1, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.TCP6, msg.ProxiedProtocol());
            Assert.Equal("2001:0db8:85a3:0000:0000:8a2e:0370:7334", msg.SourceAddress());
            Assert.Equal("1050:0:0:0:5:600:300c:326b", msg.DestinationAddress());
            Assert.Equal(56324, msg.SourcePort());
            Assert.Equal(443, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestUnknownProtocolDecode()
        {
            int startChannels = ch.Pipeline.GetEnumerator().Count();
            string header = "PROXY UNKNOWN 192.168.0.1 192.168.0.11 56324 443\r\n";
            ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V1, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.UNKNOWN, msg.ProxiedProtocol());
            Assert.Null(msg.SourceAddress());
            Assert.Null(msg.DestinationAddress());
            Assert.Equal(0, msg.SourcePort());
            Assert.Equal(0, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestV1NoUDP()
        {
            string header = "PROXY UDP4 192.168.0.1 192.168.0.11 56324 443\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestInvalidPort()
        {
            string header = "PROXY TCP4 192.168.0.1 192.168.0.11 80000 443\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestInvalidIPV4Address()
        {
            string header = "PROXY TCP4 299.168.0.1 192.168.0.11 56324 443\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestInvalidIPV6Address()
        {
            string header = "PROXY TCP6 r001:0db8:85a3:0000:0000:8a2e:0370:7334 1050:0:0:0:5:600:300c:326b 56324 443\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestInvalidProtocol()
        {
            string header = "PROXY TCP7 192.168.0.1 192.168.0.11 56324 443\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestMissingParams()
        {
            string header = "PROXY TCP4 192.168.0.1 192.168.0.11 56324\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestTooManyParams()
        {
            string header = "PROXY TCP4 192.168.0.1 192.168.0.11 56324 443 123\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestInvalidCommand()
        {
            string header = "PING TCP4 192.168.0.1 192.168.0.11 56324 443\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestInvalidEOL()
        {
            string header = "PROXY TCP4 192.168.0.1 192.168.0.11 56324 443\nGET / HTTP/1.1\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestHeaderTooLong()
        {
            string header = "PROXY TCP4 192.168.0.1 192.168.0.11 56324 " +
                            "00000000000000000000000000000000000000000000000000000000000000000443\r\n";
            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII)));
        }

        [Fact]
        public void TestIncompleteHeader()
        {
            string header = "PROXY TCP4 192.168.0.1 192.168.0.11 56324";
            ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII));
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestCloseOnInvalid()
        {
            string header = "GET / HTTP/1.1\r\n";
            try
            {
                ch.WriteInbound(CopiedBuffer(header, Encoding.ASCII));
            }
            catch (HAProxyProtocolException ppex)
            {
                // swallow this exception since we're just testing to be sure the channel was closed
            }
            bool isComplete = ch.CloseCompletion.Wait(5000);
            if (!isComplete || !ch.CloseCompletion.IsCompleted || ch.CloseCompletion.IsFaulted)
            {
                Assert.True(false, "Expected channel close");
            }
        }

        [Fact]
        public void TestTransportProtocolAndAddressFamily()
        {
            byte unknown = HAProxyProxiedProtocol.UNKNOWN.ByteValue();
            byte tcp4 = HAProxyProxiedProtocol.TCP4.ByteValue();
            byte tcp6 = HAProxyProxiedProtocol.TCP6.ByteValue();
            byte udp4 = HAProxyProxiedProtocol.UDP4.ByteValue();
            byte udp6 = HAProxyProxiedProtocol.UDP6.ByteValue();
            byte unix_stream = HAProxyProxiedProtocol.UNIX_STREAM.ByteValue();
            byte unix_dgram = HAProxyProxiedProtocol.UNIX_DGRAM.ByteValue();

            Assert.Equal(TransportProtocol.UNSPEC, TransportProtocol.ValueOf(unknown));
            Assert.Equal(TransportProtocol.STREAM, TransportProtocol.ValueOf(tcp4));
            Assert.Equal(TransportProtocol.STREAM, TransportProtocol.ValueOf(tcp6));
            Assert.Equal(TransportProtocol.STREAM, TransportProtocol.ValueOf(unix_stream));
            Assert.Equal(TransportProtocol.DGRAM, TransportProtocol.ValueOf(udp4));
            Assert.Equal(TransportProtocol.DGRAM, TransportProtocol.ValueOf(udp6));
            Assert.Equal(TransportProtocol.DGRAM, TransportProtocol.ValueOf(unix_dgram));

            Assert.Equal(AddressFamily.AF_UNSPEC, AddressFamily.ValueOf(unknown));
            Assert.Equal(AddressFamily.AF_IPv4, AddressFamily.ValueOf(tcp4));
            Assert.Equal(AddressFamily.AF_IPv4, AddressFamily.ValueOf(udp4));
            Assert.Equal(AddressFamily.AF_IPv6, AddressFamily.ValueOf(tcp6));
            Assert.Equal(AddressFamily.AF_IPv6, AddressFamily.ValueOf(udp6));
            Assert.Equal(AddressFamily.AF_UNIX, AddressFamily.ValueOf(unix_stream));
            Assert.Equal(AddressFamily.AF_UNIX, AddressFamily.ValueOf(unix_dgram));
        }

        [Fact]
        public void TestV2IPV4Decode()
        {
            byte[] header = new byte[28];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x11; // TCP over IPv4

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x0c; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            header[26] = 0x01; // Destination Port
            header[27] = (byte)0xbb; // -----

            int startChannels = ch.Pipeline.GetEnumerator().Count();
            ch.WriteInbound(Unpooled.CopiedBuffer(header));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V2, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.TCP4, msg.ProxiedProtocol());
            Assert.Equal("192.168.0.1", msg.SourceAddress());
            Assert.Equal("192.168.0.11", msg.DestinationAddress());
            Assert.Equal(56324, msg.SourcePort());
            Assert.Equal(443, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestV2UDPDecode()
        {
            byte[] header = new byte[28];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x12; // UDP over IPv4

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x0c; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            header[26] = 0x01; // Destination Port
            header[27] = (byte)0xbb; // -----

            int startChannels = ch.Pipeline.GetEnumerator().Count();
            ch.WriteInbound(Unpooled.CopiedBuffer(header));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V2, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.UDP4, msg.ProxiedProtocol());
            Assert.Equal("192.168.0.1", msg.SourceAddress());
            Assert.Equal("192.168.0.11", msg.DestinationAddress());
            Assert.Equal(56324, msg.SourcePort());
            Assert.Equal(443, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void Testv2IPV6Decode()
        {
            byte[] header = new byte[52];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x21; // TCP over IPv6

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x24; // -----

            header[16] = 0x20; // Source Address
            header[17] = 0x01; // -----
            header[18] = 0x0d; // -----
            header[19] = (byte)0xb8; // -----
            header[20] = (byte)0x85; // -----
            header[21] = (byte)0xa3; // -----
            header[22] = 0x00; // -----
            header[23] = 0x00; // -----
            header[24] = 0x00; // -----
            header[25] = 0x00; // -----
            header[26] = (byte)0x8a; // -----
            header[27] = 0x2e; // -----
            header[28] = 0x03; // -----
            header[29] = 0x70; // -----
            header[30] = 0x73; // -----
            header[31] = 0x34; // -----

            header[32] = 0x10; // Destination Address
            header[33] = 0x50; // -----
            header[34] = 0x00; // -----
            header[35] = 0x00; // -----
            header[36] = 0x00; // -----
            header[37] = 0x00; // -----
            header[38] = 0x00; // -----
            header[39] = 0x00; // -----
            header[40] = 0x00; // -----
            header[41] = 0x05; // -----
            header[42] = 0x06; // -----
            header[43] = 0x00; // -----
            header[44] = 0x30; // -----
            header[45] = 0x0c; // -----
            header[46] = 0x32; // -----
            header[47] = 0x6b; // -----

            header[48] = (byte)0xdc; // Source Port
            header[49] = 0x04; // -----

            header[50] = 0x01; // Destination Port
            header[51] = (byte)0xbb; // -----

            int startChannels = ch.Pipeline.GetEnumerator().Count();
            ch.WriteInbound(Unpooled.CopiedBuffer(header));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V2, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.TCP6, msg.ProxiedProtocol());
            Assert.Equal("2001:db8:85a3:0:0:8a2e:370:7334", msg.SourceAddress());
            Assert.Equal("1050:0:0:0:5:600:300c:326b", msg.DestinationAddress());
            Assert.Equal(56324, msg.SourcePort());
            Assert.Equal(443, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void Testv2UnixDecode()
        {
            byte[] header = new byte[232];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x31; // UNIX_STREAM

            header[14] = 0x00; // Remaining Bytes
            header[15] = (byte)0xd8; // -----

            header[16] = 0x2f; // Source Address
            header[17] = 0x76; // -----
            header[18] = 0x61; // -----
            header[19] = 0x72; // -----
            header[20] = 0x2f; // -----
            header[21] = 0x72; // -----
            header[22] = 0x75; // -----
            header[23] = 0x6e; // -----
            header[24] = 0x2f; // -----
            header[25] = 0x73; // -----
            header[26] = 0x72; // -----
            header[27] = 0x63; // -----
            header[28] = 0x2e; // -----
            header[29] = 0x73; // -----
            header[30] = 0x6f; // -----
            header[31] = 0x63; // -----
            header[32] = 0x6b; // -----
            header[33] = 0x00; // -----

            header[124] = 0x2f; // Destination Address
            header[125] = 0x76; // -----
            header[126] = 0x61; // -----
            header[127] = 0x72; // -----
            header[128] = 0x2f; // -----
            header[129] = 0x72; // -----
            header[130] = 0x75; // -----
            header[131] = 0x6e; // -----
            header[132] = 0x2f; // -----
            header[133] = 0x64; // -----
            header[134] = 0x65; // -----
            header[135] = 0x73; // -----
            header[136] = 0x74; // -----
            header[137] = 0x2e; // -----
            header[138] = 0x73; // -----
            header[139] = 0x6f; // -----
            header[140] = 0x63; // -----
            header[141] = 0x6b; // -----
            header[142] = 0x00; // -----

            int startChannels = ch.Pipeline.GetEnumerator().Count();
            ch.WriteInbound(Unpooled.CopiedBuffer(header));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V2, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.UNIX_STREAM, msg.ProxiedProtocol());
            Assert.Equal("/var/run/src.sock", msg.SourceAddress());
            Assert.Equal("/var/run/dest.sock", msg.DestinationAddress());
            Assert.Equal(0, msg.SourcePort());
            Assert.Equal(0, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestV2LocalProtocolDecode()
        {
            byte[] header = new byte[28];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x20; // v2, cmd=LOCAL
            header[13] = 0x00; // Unspecified transport protocol and address family

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x0c; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            header[26] = 0x01; // Destination Port
            header[27] = (byte)0xbb; // -----

            int startChannels = ch.Pipeline.GetEnumerator().Count();
            ch.WriteInbound(Unpooled.CopiedBuffer(header));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V2, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.LOCAL, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.UNKNOWN, msg.ProxiedProtocol());
            Assert.Null(msg.SourceAddress());
            Assert.Null(msg.DestinationAddress());
            Assert.Equal(0, msg.SourcePort());
            Assert.Equal(0, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestV2UnknownProtocolDecode()
        {
            byte[] header = new byte[28];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x00; // Unspecified transport protocol and address family

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x0c; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            header[26] = 0x01; // Destination Port
            header[27] = (byte)0xbb; // -----

            int startChannels = ch.Pipeline.GetEnumerator().Count();
            ch.WriteInbound(Unpooled.CopiedBuffer(header));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V2, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.UNKNOWN, msg.ProxiedProtocol());
            Assert.Null(msg.SourceAddress());
            Assert.Null(msg.DestinationAddress());
            Assert.Equal(0, msg.SourcePort());
            Assert.Equal(0, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestV2WithSslTLVs()
        {
            ch = new EmbeddedChannel(new HAProxyMessageDecoder());

            byte[] bytes = {
                    13, 10, 13, 10, 0, 13, 10, 81, 85, 73, 84, 10, 33, 17, 0, 35, 127, 0, 0, 1, 127, 0, 0, 1,
                    201, 166, 7, 89, 32, 0, 20, 5, 0, 0, 0, 0, 33, 0, 5, 84, 76, 83, 118, 49, 34, 0, 4, 76, 69, 65, 70
            };//-55, -90

            int startChannels = ch.Pipeline.GetEnumerator().Count();
            Assert.True(ch.WriteInbound(Unpooled.CopiedBuffer(bytes)));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            HAProxyMessage msg = (HAProxyMessage)msgObj;

            Assert.Equal(HAProxyProtocolVersion.V2, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.TCP4, msg.ProxiedProtocol());
            Assert.Equal("127.0.0.1", msg.SourceAddress());
            Assert.Equal("127.0.0.1", msg.DestinationAddress());
            Assert.Equal(51622, msg.SourcePort());
            Assert.Equal(1881, msg.DestinationPort());
            IList<HAProxyTLV> tlvs = msg.Tlvs();

            Assert.Equal(3, tlvs.Count);
            HAProxyTLV firstTlv = tlvs[0];
            Assert.Equal(HAProxyTLV.Type.PP2_TYPE_SSL, firstTlv.TLVType());
            HAProxySSLTLV sslTlv = (HAProxySSLTLV) firstTlv;
            Assert.Equal(0, sslTlv.Verify());
            Assert.True(sslTlv.IsPP2ClientSSL());
            Assert.True(sslTlv.IsPP2ClientCertSess());
            Assert.False(sslTlv.IsPP2ClientCertConn());

            HAProxyTLV secondTlv = tlvs[1];

            Assert.Equal(HAProxyTLV.Type.PP2_TYPE_SSL_VERSION, secondTlv.TLVType());
            IByteBuffer secondContentBuf = secondTlv.Content;
            byte[] secondContent = new byte[secondContentBuf.ReadableBytes];
            secondContentBuf.ReadBytes(secondContent);
            Assert.Equal(Encoding.ASCII.GetBytes("TLSv1"), secondContent);

            HAProxyTLV thirdTLV = tlvs[2];
            Assert.Equal(HAProxyTLV.Type.PP2_TYPE_SSL_CN, thirdTLV.TLVType());
            IByteBuffer thirdContentBuf = thirdTLV.Content;
            byte[] thirdContent = new byte[thirdContentBuf.ReadableBytes];
            thirdContentBuf.ReadBytes(thirdContent);
            Assert.Equal(Encoding.ASCII.GetBytes("LEAF"), thirdContent);

            Assert.True(sslTlv.EncapsulatedTLVs().Contains(secondTlv));
            Assert.True(sslTlv.EncapsulatedTLVs().Contains(thirdTLV));

            Assert.True(0 < firstTlv.ReferenceCount);
            Assert.True(0 < secondTlv.ReferenceCount);
            Assert.True(0 < thirdTLV.ReferenceCount);
            Assert.False(thirdTLV.Release());
            Assert.False(secondTlv.Release());
            Assert.True(firstTlv.Release());
            Assert.Equal(0, firstTlv.ReferenceCount);
            Assert.Equal(0, secondTlv.ReferenceCount);
            Assert.Equal(0, thirdTLV.ReferenceCount);

            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestV2WithTLV()
        {
            ch = new EmbeddedChannel(new HAProxyMessageDecoder(4));

            byte[] header = new byte[236];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x31; // UNIX_STREAM

            header[14] = 0x00; // Remaining Bytes
            header[15] = (byte)0xdc; // -----

            header[16] = 0x2f; // Source Address
            header[17] = 0x76; // -----
            header[18] = 0x61; // -----
            header[19] = 0x72; // -----
            header[20] = 0x2f; // -----
            header[21] = 0x72; // -----
            header[22] = 0x75; // -----
            header[23] = 0x6e; // -----
            header[24] = 0x2f; // -----
            header[25] = 0x73; // -----
            header[26] = 0x72; // -----
            header[27] = 0x63; // -----
            header[28] = 0x2e; // -----
            header[29] = 0x73; // -----
            header[30] = 0x6f; // -----
            header[31] = 0x63; // -----
            header[32] = 0x6b; // -----
            header[33] = 0x00; // -----

            header[124] = 0x2f; // Destination Address
            header[125] = 0x76; // -----
            header[126] = 0x61; // -----
            header[127] = 0x72; // -----
            header[128] = 0x2f; // -----
            header[129] = 0x72; // -----
            header[130] = 0x75; // -----
            header[131] = 0x6e; // -----
            header[132] = 0x2f; // -----
            header[133] = 0x64; // -----
            header[134] = 0x65; // -----
            header[135] = 0x73; // -----
            header[136] = 0x74; // -----
            header[137] = 0x2e; // -----
            header[138] = 0x73; // -----
            header[139] = 0x6f; // -----
            header[140] = 0x63; // -----
            header[141] = 0x6b; // -----
            header[142] = 0x00; // -----

            // ---- Additional data (TLV) ---- \\

            header[232] = 0x01; // Type
            header[233] = 0x00; // Remaining bytes
            header[234] = 0x01; // -----
            header[235] = 0x01; // Payload

            int startChannels = ch.Pipeline.GetEnumerator().Count();
            ch.WriteInbound(Unpooled.CopiedBuffer(header));
            object msgObj = ch.ReadInbound<object>();
            Assert.Equal(startChannels - 1, ch.Pipeline.GetEnumerator().Count());
            Assert.True(msgObj is HAProxyMessage);
            HAProxyMessage msg = (HAProxyMessage)msgObj;
            Assert.Equal(HAProxyProtocolVersion.V2, msg.ProtocolVersion());
            Assert.Equal(HAProxyCommand.PROXY, msg.Command());
            Assert.Equal(HAProxyProxiedProtocol.UNIX_STREAM, msg.ProxiedProtocol());
            Assert.Equal("/var/run/src.sock", msg.SourceAddress());
            Assert.Equal("/var/run/dest.sock", msg.DestinationAddress());
            Assert.Equal(0, msg.SourcePort());
            Assert.Equal(0, msg.DestinationPort());
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestV2InvalidProtocol()
        {
            byte[] header = new byte[28];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x41; // Bogus transport protocol

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x0c; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            header[26] = 0x01; // Destination Port
            header[27] = (byte)0xbb; // -----

            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(Unpooled.CopiedBuffer(header)));
        }

        [Fact]
        public void TestV2MissingParams()
        {
            byte[] header = new byte[26];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x11; // TCP over IPv4

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x0a; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(Unpooled.CopiedBuffer(header)));
        }

        [Fact]
        public void TestV2InvalidCommand()
        {
            byte[] header = new byte[28];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x22; // v2, Bogus command
            header[13] = 0x11; // TCP over IPv4

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x0c; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            header[26] = 0x01; // Destination Port
            header[27] = (byte)0xbb; // -----

            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(Unpooled.CopiedBuffer(header)));
        }

        [Fact]
        public void TestV2InvalidVersion()
        {
            byte[] header = new byte[28];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x31; // Bogus version, cmd=PROXY
            header[13] = 0x11; // TCP over IPv4

            header[14] = 0x00; // Remaining Bytes
            header[15] = 0x0c; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            header[26] = 0x01; // Destination Port
            header[27] = (byte)0xbb; // -----

            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(Unpooled.CopiedBuffer(header)));
        }

        [Fact]
        public void TestV2HeaderTooLong()
        {
            ch = new EmbeddedChannel(new HAProxyMessageDecoder(0));

            byte[] header = new byte[248];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY
            header[13] = 0x11; // TCP over IPv4

            header[14] = 0x00; // Remaining Bytes
            header[15] = (byte)0xe8; // -----

            header[16] = (byte)0xc0; // Source Address
            header[17] = (byte)0xa8; // -----
            header[18] = 0x00; // -----
            header[19] = 0x01; // -----

            header[20] = (byte)0xc0; // Destination Address
            header[21] = (byte)0xa8; // -----
            header[22] = 0x00; // -----
            header[23] = 0x0b; // -----

            header[24] = (byte)0xdc; // Source Port
            header[25] = 0x04; // -----

            header[26] = 0x01; // Destination Port
            header[27] = (byte)0xbb; // -----

            Assert.Throws<HAProxyProtocolException>(() => ch.WriteInbound(Unpooled.CopiedBuffer(header)));
        }

        [Fact]
        public void TestV2IncompleteHeader()
        {
            byte[] header = new byte[13];
            header[0] = 0x0D; // Binary Prefix
            header[1] = 0x0A; // -----
            header[2] = 0x0D; // -----
            header[3] = 0x0A; // -----
            header[4] = 0x00; // -----
            header[5] = 0x0D; // -----
            header[6] = 0x0A; // -----
            header[7] = 0x51; // -----
            header[8] = 0x55; // -----
            header[9] = 0x49; // -----
            header[10] = 0x54; // -----
            header[11] = 0x0A; // -----

            header[12] = 0x21; // v2, cmd=PROXY

            ch.WriteInbound(Unpooled.CopiedBuffer(header));
            Assert.Null(ch.ReadInbound<object>());
            Assert.False(ch.Finish());
        }

        [Fact]
        public void TestDetectProtocol()
        {
            IByteBuffer validHeaderV1 = CopiedBuffer("PROXY TCP4 192.168.0.1 192.168.0.11 56324 443\r\n",
                                                       Encoding.ASCII);
            ProtocolDetectionResult<HAProxyProtocolVersion> result = HAProxyMessageDecoder.DetectProtocol(validHeaderV1);
            Assert.Equal(ProtocolDetectionState.DETECTED, result.State());
            Assert.Equal(HAProxyProtocolVersion.V1, result.DetectedProtocol());
            validHeaderV1.Release();

            IByteBuffer invalidHeader = CopiedBuffer("Invalid header", Encoding.ASCII);
            result = HAProxyMessageDecoder.DetectProtocol(invalidHeader);
            Assert.Equal(ProtocolDetectionState.INVALID, result.State());
            Assert.Null(result.DetectedProtocol());
            invalidHeader.Release();

            IByteBuffer validHeaderV2 = Unpooled.Buffer();
            validHeaderV2.WriteByte(0x0D);
            validHeaderV2.WriteByte(0x0A);
            validHeaderV2.WriteByte(0x0D);
            validHeaderV2.WriteByte(0x0A);
            validHeaderV2.WriteByte(0x00);
            validHeaderV2.WriteByte(0x0D);
            validHeaderV2.WriteByte(0x0A);
            validHeaderV2.WriteByte(0x51);
            validHeaderV2.WriteByte(0x55);
            validHeaderV2.WriteByte(0x49);
            validHeaderV2.WriteByte(0x54);
            validHeaderV2.WriteByte(0x0A);
            result = HAProxyMessageDecoder.DetectProtocol(validHeaderV2);
            Assert.Equal(ProtocolDetectionState.DETECTED, result.State());
            Assert.Equal(HAProxyProtocolVersion.V2, result.DetectedProtocol());
            validHeaderV2.Release();

            IByteBuffer incompleteHeader = Unpooled.Buffer();
            incompleteHeader.WriteByte(0x0D);
            incompleteHeader.WriteByte(0x0A);
            incompleteHeader.WriteByte(0x0D);
            incompleteHeader.WriteByte(0x0A);
            incompleteHeader.WriteByte(0x00);
            incompleteHeader.WriteByte(0x0D);
            incompleteHeader.WriteByte(0x0A);
            result = HAProxyMessageDecoder.DetectProtocol(incompleteHeader);
            Assert.Equal(ProtocolDetectionState.NEEDS_MORE_DATA, result.State());
            Assert.Null(result.DetectedProtocol());
            incompleteHeader.Release();
        }

    }
}
