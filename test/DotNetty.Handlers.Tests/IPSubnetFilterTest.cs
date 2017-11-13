// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Handlers.IPFilter;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;
    using Xunit.Abstractions;

    public class IPSubnetFilterTest : TestBase
    {
        public IPSubnetFilterTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void TestIpv4DefaultRoute()
        {
            var rule = new IPSubnetFilterRule("0.0.0.0", 0, IPFilterRuleType.Accept);
            Assert.True(rule.Matches(CreateIPEndPoint("91.114.240.43")));
            Assert.True(rule.Matches(CreateIPEndPoint("10.0.0.3")));
            Assert.True(rule.Matches(CreateIPEndPoint("192.168.93.2")));
        }

        [Fact]
        public void TestIpv4SubnetMaskCorrectlyHandlesIpv6()
        {
            var rule = new IPSubnetFilterRule("0.0.0.0", 0, IPFilterRuleType.Accept);
            Assert.False(rule.Matches(CreateIPEndPoint("2001:db8:abcd:0000::1")));
        }

        [Fact]
        public void TestIpv6SubnetMaskCorrectlyHandlesIpv4()
        {
            var rule = new IPSubnetFilterRule("::", 0, IPFilterRuleType.Accept);
            Assert.False(rule.Matches(CreateIPEndPoint("91.114.240.43")));
        }

        [Fact]
        public void TestIp4SubnetFilterRule()
        {
            var rule = new IPSubnetFilterRule("192.168.56.1", 24, IPFilterRuleType.Accept);
            for (int i = 0; i <= 255; i++)
            {
                Assert.True(rule.Matches(CreateIPEndPoint(string.Format("192.168.56.{0}", i))));
            }
            Assert.False(rule.Matches(CreateIPEndPoint("192.168.57.1")));

            rule = new IPSubnetFilterRule("91.114.240.1", 23, IPFilterRuleType.Accept);
            Assert.True(rule.Matches(CreateIPEndPoint("91.114.240.43")));
            Assert.True(rule.Matches(CreateIPEndPoint("91.114.240.255")));
            Assert.True(rule.Matches(CreateIPEndPoint("91.114.241.193")));
            Assert.True(rule.Matches(CreateIPEndPoint("91.114.241.254")));
            Assert.False(rule.Matches(CreateIPEndPoint("91.115.241.2")));
        }

        [Fact]
        public void TestIp6SubnetFilterRule()
        {
            var rule = new IPSubnetFilterRule("2001:db8:abcd:0000::", 52, IPFilterRuleType.Accept);
            Assert.True(rule.RuleType == IPFilterRuleType.Accept);
            Assert.True(rule.Matches(CreateIPEndPoint("2001:db8:abcd:0000::1")));
            Assert.True(rule.Matches(CreateIPEndPoint("2001:db8:abcd:0fff:ffff:ffff:ffff:ffff")));
            Assert.False(rule.Matches(CreateIPEndPoint("2001:db8:abcd:1000::")));
            
            
            rule = new IPSubnetFilterRule("2001:db8:1234:c000::", 50, IPFilterRuleType.Reject);
            Assert.True(rule.RuleType == IPFilterRuleType.Reject);
            Assert.True(rule.Matches(CreateIPEndPoint("2001:db8:1234:c000::")));
            Assert.True(rule.Matches(CreateIPEndPoint("2001:db8:1234:ffff:ffff:ffff:1111:ffff")));
            Assert.True(rule.Matches(CreateIPEndPoint("2001:db8:1234:ffff:ffff:ffff:ffff:ffff")));
            Assert.False(rule.Matches(CreateIPEndPoint("2001:db8:1234:bfff:ffff:ffff:ffff:ffff")));
            Assert.False(rule.Matches(CreateIPEndPoint("2001:db8:1234:8000::")));
            Assert.False(rule.Matches(CreateIPEndPoint("2001:db7:1234:c000::")));
        }

        [Fact]
        public void TestIPFilterRuleHandler()
        {
            
            IIPFilterRule filter0 = new TestIPFilterRule(TestIPFilterRuleHandlerConstants.IP1);
            RuleBasedIPFilter denyHandler = new TestDenyFilter(TestIPFilterRuleHandlerConstants.IP1, filter0);
            EmbeddedChannel chDeny = new TestIpFilterRuleHandlerChannel1(denyHandler);
            var output = chDeny.ReadOutbound<IByteBuffer>();
            Assert.Equal(7, output.ReadableBytes);
            for (byte i = 1; i <= 7; i++)
            {
                Assert.Equal(i, output.ReadByte());
            }
            //waiting finish of ContinueWith for chDeny.ChannelRejected
            Thread.Sleep(300);
            Assert.False(chDeny.Active);
            Assert.False(chDeny.Open);
            RuleBasedIPFilter allowHandler = new TestAllowFilter(filter0);
            EmbeddedChannel chAllow = new TestIpFilterRuleHandlerChannel2(allowHandler);
            Assert.True(chAllow.Active);
            Assert.True(chAllow.Open);
        }
        
        [Fact]
        public void TestUniqueIPFilterHandler() {
            var handler = new UniqueIPFilter();

            EmbeddedChannel ch1 = new TestUniqueIPFilterHandlerChannel1(handler);
            Assert.True(ch1.Active);
            EmbeddedChannel ch2 = new TestUniqueIPFilterHandlerChannel2(handler);
            Assert.True(ch2.Active);
            EmbeddedChannel ch3 =new TestUniqueIPFilterHandlerChannel1( handler);
            Assert.False(ch3.Active);

            // false means that no data is left to read/write
            Assert.False(ch1.Finish());
            
            //waiting finish of ContinueWith for ch1.CloseCompletion
            Thread.Sleep(300);

            EmbeddedChannel ch4 = new TestUniqueIPFilterHandlerChannel1(handler);
            Assert.True(ch4.Active);
        }

        #region private
        
        private static class TestIPFilterRuleHandlerConstants
        {
            public const string IP1 = "192.168.57.1";
            public const string IP2 = "192.168.57.2";
        }
        
        private static class TestUniqueIPFilterHandlerConstants
        {
            public const string IP1 = "91.92.93.1";
            public const string IP2 = "91.92.93.2";
        }
        

        private class TestIPFilterRule : IIPFilterRule
        {
            readonly string ip;

            public TestIPFilterRule(string ip)
            {
                this.ip = ip;
            }

            public bool Matches(IPEndPoint remoteAddress)
            {
                return this.ip.Equals(remoteAddress.Address.ToString());
            }

            public IPFilterRuleType RuleType => IPFilterRuleType.Reject;
        }

        private class TestDenyFilter : RuleBasedIPFilter
        {
            readonly string ip;
            readonly byte[] message = { 1, 2, 3, 4, 5, 6, 7 };

            public TestDenyFilter(string ip, IIPFilterRule rule)
                : base(rule)
            {
                this.ip = ip;
            }

            protected override Task ChannelRejected(IChannelHandlerContext ctx, IPEndPoint remoteAddress)
            {
                Assert.True(ctx.Channel.Active);
                Assert.True(ctx.Channel.IsWritable);
                Assert.Equal(this.ip, remoteAddress.Address.ToString());
                return ctx.WriteAndFlushAsync(Unpooled.WrappedBuffer(this.message));
            }
        }

        private class TestAllowFilter : RuleBasedIPFilter
        {
            public TestAllowFilter(IIPFilterRule rule)
                : base(rule)
            {
            }

            protected override Task ChannelRejected(IChannelHandlerContext ctx, IPEndPoint remoteAddress)
            {
                throw new InvalidOperationException("This code must be skipped during test execution.");
            }
        }

        private class TestUniqueIPFilterHandlerChannel1 : EmbeddedChannel
        {
            public TestUniqueIPFilterHandlerChannel1(params IChannelHandler[] handlers):base(handlers)
            {
            }

            protected override EndPoint RemoteAddressInternal => this.Active 
                ? CreateIPEndPoint(TestUniqueIPFilterHandlerConstants.IP1, 5421) 
                : null;
        }
        
        private class TestUniqueIPFilterHandlerChannel2 : EmbeddedChannel
        {
            public TestUniqueIPFilterHandlerChannel2(params IChannelHandler[] handlers):base(handlers)
            {
            }

            protected override EndPoint RemoteAddressInternal => this.Active 
                ? CreateIPEndPoint(TestUniqueIPFilterHandlerConstants.IP2, 5421) 
                : null;
        }
        
        private class TestIpFilterRuleHandlerChannel1 : EmbeddedChannel
        {
            public TestIpFilterRuleHandlerChannel1(params IChannelHandler[] handlers):base(handlers)
            {
            }

            protected override EndPoint RemoteAddressInternal => this.Active 
                ? CreateIPEndPoint(TestIPFilterRuleHandlerConstants.IP1, 5421) 
                : null;
        }
        
        private class TestIpFilterRuleHandlerChannel2 : EmbeddedChannel
        {
            public TestIpFilterRuleHandlerChannel2(params IChannelHandler[] handlers):base(handlers)
            {
            }

            protected override EndPoint RemoteAddressInternal => this.Active 
                ? CreateIPEndPoint(TestIPFilterRuleHandlerConstants.IP2, 5421) 
                : null;
        }

        static IPEndPoint CreateIPEndPoint(string ipAddress, int port = 1234)
        {
            return new IPEndPoint(IPAddress.Parse(ipAddress), port);
        }

        #endregion
    }
}