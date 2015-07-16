// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.End2End
{
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
    using Xunit;

    public class DiagnosticsTests
    {
        [Fact]
        public void VerifyEventSources()
        {
            EventSourceAnalyzer.InspectAll(ExecutorEventSource.Log);
            EventSourceAnalyzer.InspectAll(ChannelEventSource.Log);
            EventSourceAnalyzer.InspectAll(BootstrapEventSource.Log);
            EventSourceAnalyzer.InspectAll(MqttEventSource.Log);
        }
    }
}