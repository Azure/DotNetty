// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class ReadOnlyByteBufferStreamTests
    {
        readonly TestByteBuffer testBuffer = new TestByteBuffer();

        [Fact]
        public void CanReadCountBytesIntoBuffer()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];
            int read = stream.Read(output, 0, output.Length);

            Assert.Equal(4, read);
            Assert.Equal(new byte[] {42, 42, 42, 42}, output);
        }

        [Fact]
        public void CanReadCountBytesIntoBufferAtOffset()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];
            int read = stream.Read(output, 1, 2);

            Assert.Equal(2, read);
            Assert.Equal(new byte[] { 0, 42, 42, 0 }, output);
        }

        [Fact]
        public void CanGetTheStreamLength()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            Assert.Equal(this.testBuffer.WriterIndex, stream.Length);
        }

        [Fact]
        public void CanGetConsistentStreamLengthAcrossReads()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];

            stream.Read(output, 0, 4);
            Assert.Equal(100, stream.Length);

            stream.Read(output, 0, 4);
            Assert.Equal(100, stream.Length);
        }
    }
}