// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using DotNetty.Common.Internal.Logging;
    using Xunit.Abstractions;

    public abstract class TestBase
    {
        protected readonly ITestOutputHelper Output;

        protected TestBase(ITestOutputHelper output)
        {
            this.Output = output;
            InternalLoggerFactory.DefaultFactory.AddProvider(new XUnitOutputLoggerProvider(output));
        }
    }
}