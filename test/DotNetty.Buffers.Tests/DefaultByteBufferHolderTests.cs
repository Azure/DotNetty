// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class DefaultByteBufferHolderTests
    {
        [Fact]
        public void ConvertToString()
        {
            var holder = new DefaultByteBufferHolder(Unpooled.Buffer());
            Assert.Equal(1, holder.ReferenceCount);
            Assert.NotNull(holder.ToString());
            Assert.True(holder.Release());
            Assert.NotNull(holder.ToString());
        }

        [Fact]
        public void EqualsAndHashCode()
        {
            var holder = new DefaultByteBufferHolder(Unpooled.Empty);
            IByteBufferHolder copy = holder.Copy();
            try
            {
                Assert.Equal(holder, copy);
                Assert.Equal(holder.GetHashCode(), copy.GetHashCode());
            }
            finally
            {
                holder.Release();
                copy.Release();
            }
        }
    }
}
