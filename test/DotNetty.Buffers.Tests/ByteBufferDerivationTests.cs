// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class ByteBufferDerivationTests
    {
        [Fact]
        public void TestSwap()
        {
            IByteBuffer buf = Unpooled.Buffer(8).SetIndex(1, 7);
            IByteBuffer swapped = buf.WithOrder(ByteOrder.LittleEndian);

            Assert.IsType<SwappedByteBuffer>(swapped);
            Assert.Null(swapped.Unwrap());
            Assert.Same(buf, swapped.WithOrder(ByteOrder.BigEndian));
            Assert.Same(swapped, swapped.WithOrder(ByteOrder.LittleEndian));

            buf.SetIndex(2, 6);
            Assert.Equal(swapped.ReaderIndex, 2);
            Assert.Equal(swapped.WriterIndex, 6);
        }
    }
}