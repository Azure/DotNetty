// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    public class WrappedCompositeByteBufferTests : CompositeByteBufferTests
    {
        protected sealed override IByteBuffer NewBuffer(int length, int maxCapacity) => this.Wrap((CompositeByteBuffer)base.NewBuffer(length, maxCapacity));

        protected virtual IByteBuffer Wrap(CompositeByteBuffer buffer) => new WrappedCompositeByteBuffer(buffer);
    }
}
