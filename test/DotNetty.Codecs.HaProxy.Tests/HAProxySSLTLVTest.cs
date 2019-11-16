// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.HaProxy.Tests
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using Xunit;

    public class HAProxySSLTLVTest
    {

        [Fact]
        public void TestClientBitmask()
        {
            // 0b0000_0111
            byte allClientsEnabled = 0x7;
            HAProxySSLTLV allClientsEnabledTLV =
                    new HAProxySSLTLV(0, allClientsEnabled, new List<HAProxyTLV>(), Unpooled.Buffer());

            Assert.True(allClientsEnabledTLV.IsPP2ClientCertConn());
            Assert.True(allClientsEnabledTLV.IsPP2ClientSSL());
            Assert.True(allClientsEnabledTLV.IsPP2ClientCertSess());

            Assert.True(allClientsEnabledTLV.Release());

            // 0b0000_0101
            byte clientSSLandClientCertSessEnabled = 0x5;

            HAProxySSLTLV clientSSLandClientCertSessTLV =
                    new HAProxySSLTLV(0, clientSSLandClientCertSessEnabled, new List<HAProxyTLV>(), Unpooled.Buffer());

            Assert.False(clientSSLandClientCertSessTLV.IsPP2ClientCertConn());
            Assert.True(clientSSLandClientCertSessTLV.IsPP2ClientSSL());
            Assert.True(clientSSLandClientCertSessTLV.IsPP2ClientCertSess());

            Assert.True(clientSSLandClientCertSessTLV.Release());
            // 0b0000_0000
            byte noClientEnabled = 0x0;

            HAProxySSLTLV noClientTlv =
                    new HAProxySSLTLV(0, noClientEnabled, new List<HAProxyTLV>(), Unpooled.Buffer());

            Assert.False(noClientTlv.IsPP2ClientCertConn());
            Assert.False(noClientTlv.IsPP2ClientSSL());
            Assert.False(noClientTlv.IsPP2ClientCertSess());

            Assert.True(noClientTlv.Release());
        }
    }
}
