// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.End2End
{
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
    using Xunit;

    public class DiagnosticsTests
    {
        [Fact]
        public void VerifyEventSources()
        {
            EventSourceAnalyzer.InspectAll(DefaultEventSource.Log);
        }
    }
}