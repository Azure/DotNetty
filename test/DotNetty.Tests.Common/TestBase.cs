// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using System.Diagnostics.Tracing;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
    using Xunit.Abstractions;

    public abstract class TestBase : IDisposable
    {
        protected readonly ITestOutputHelper Output;
        readonly ObservableEventListener eventListener;

        protected TestBase(ITestOutputHelper output)
        {
            this.Output = output;
            this.eventListener = new ObservableEventListener();
            this.eventListener.LogToTestOutput(output);
            this.eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);
        }

        public void Dispose()
        {
            this.eventListener.Dispose();
        }
    }
}