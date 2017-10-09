// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class UnpooledWriteStreamTests
    {
        [Fact]
        public async Task WriteBytesAsyncPartialWrite()
        {
            const int CopyLength = 200 * 1024;
            const int SourceLength = 300 * 1024;
            const int BufferCapacity = 400 * 1024;

            var bytes = new byte[SourceLength];
            var random = new Random(Guid.NewGuid().GetHashCode());
            random.NextBytes(bytes);

            IByteBuffer buffer = Unpooled.Buffer(BufferCapacity);
            int initialWriterIndex = buffer.WriterIndex;
            using (var stream = new PortionedMemoryStream(bytes, Enumerable.Repeat(1, int.MaxValue).Select(_ => random.Next(1, 10240))))
            {
                await buffer.WriteBytesAsync(stream, CopyLength);
            }
            Assert.Equal(CopyLength, buffer.WriterIndex - initialWriterIndex);
            Assert.True(ByteBufferUtil.Equals(Unpooled.WrappedBuffer(bytes.Slice(0, CopyLength)), buffer));
        }
    }
}