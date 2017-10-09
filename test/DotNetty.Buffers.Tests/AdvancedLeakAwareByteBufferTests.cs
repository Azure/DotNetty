﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using DotNetty.Common;

    public class AdvancedLeakAwareByteBufferTests : SimpleLeakAwareByteBufferTests
    {
        protected override Type ByteBufferType => typeof(AdvancedLeakAwareByteBuffer);

        protected override IByteBuffer Wrap(IByteBuffer buffer, IResourceLeakTracker tracker) => new AdvancedLeakAwareByteBuffer(buffer, tracker);
    }
}
