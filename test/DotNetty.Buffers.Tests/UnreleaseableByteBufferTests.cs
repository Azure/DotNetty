// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class UnreleaseableByteBufferTests
    {
        [Fact]
        public void CantRelease()
        {
            IByteBuffer buffer = Unpooled.UnreleasableBuffer(Unpooled.Buffer(1));

            Assert.Equal(1, buffer.ReferenceCount);
            Assert.False(buffer.Release());
            Assert.Equal(1, buffer.ReferenceCount);
            Assert.False(buffer.Release());
            Assert.Equal(1, buffer.ReferenceCount);

            buffer.Retain(5);
            Assert.Equal(1, buffer.ReferenceCount);

            buffer.Retain();
            Assert.Equal(1, buffer.ReferenceCount);

            Assert.True(buffer.Unwrap().Release());
            Assert.Equal(0, buffer.ReferenceCount);
        }
    }
}
