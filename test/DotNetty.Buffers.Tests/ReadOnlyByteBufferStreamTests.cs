// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.IO;
    using Xunit;

    public class ReadOnlyByteBufferStreamTests
    {
        readonly TestByteBuffer testBuffer = new TestByteBuffer();

        [Fact]
        public void StreamIsSeekable()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            Assert.True(stream.CanSeek);
        }

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
        public void CanDoMultipleReadsFromStreamIntoBuffer()
        {
            var streamBuffer = new TestByteBuffer(4);
            var stream = new ReadOnlyByteBufferStream(streamBuffer, false);
            var output = new byte[4];
            stream.Read(output, 0, 2);
            int read = stream.Read(output, 2, 2);

            Assert.Equal(2, read);
            Assert.Equal(new byte[] { 42, 42, 42, 42 }, output);
        }

        [Fact]
        public void SingleReadCannotPassTheEndOfTheStream()
        {
            var streamBuffer = new TestByteBuffer(4);
            var stream = new ReadOnlyByteBufferStream(streamBuffer, false);
            var output = new byte[6];

            // single read is too big for the stream
            stream.Read(output, 0, 6);

            Assert.Equal(new byte[] { 42, 42, 42, 42, 0, 0 }, output);
        }

        [Fact]
        public void MultiReadCannotPassTheEndOfTheStream()
        {
            var streamBuffer = new TestByteBuffer(4);
            var stream = new ReadOnlyByteBufferStream(streamBuffer, false);
            var output = new byte[6];

            // 2nd read is too big for the stream
            stream.Read(output, 0, 2);
            stream.Read(output, 0, 4);

            Assert.Equal(new byte[] { 42, 42, 0, 0, 0, 0 }, output);
        }

        [Fact]
        public void SingleReadCannotWritePastTheEndOfTheDestinationBuffer()
        {
            var streamBuffer = new TestByteBuffer(6);
            var stream = new ReadOnlyByteBufferStream(streamBuffer, false);
            var output = new byte[4];

            // single read is too big for the output buffer
            Assert.Throws<ArgumentException>(() => stream.Read(output, 0, 6));
        }

        [Fact]
        public void MultiReadCannotWritePastTheEndOfTheDestinationBuffer()
        {
            var streamBuffer = new TestByteBuffer(6);
            var stream = new ReadOnlyByteBufferStream(streamBuffer, false);
            var output = new byte[4];

            // 2nd read is too big for the output buffer
            stream.Read(output, 0, 2);
            Assert.Throws<ArgumentException>(() => stream.Read(output, 2, 4));
        }

        [Fact]
        public void ReadZeroBytesFromTheEndOfTheStream()
        {
            var streamBuffer = new TestByteBuffer(4);
            var stream = new ReadOnlyByteBufferStream(streamBuffer, false);
            var output = new byte[4];

            stream.Read(output, 0, 4);
            int read = stream.Read(output, 0, 4);

            Assert.Equal(0, read);
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

        [Fact]
        public void CanGetTheStreamPosition()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);

            Assert.Equal(0, stream.Position);

            var output = new byte[4];
            int read = stream.Read(output, 0, 2);

            Assert.Equal(2, stream.Position);
        }

        [Fact]
        public void CanSetTheStreamPositionWithinTheBufferBounds()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);

            // end
            stream.Position = this.testBuffer.WriterIndex;
            Assert.Equal(this.testBuffer.WriterIndex, stream.Position);

            // middle
            stream.Position = this.testBuffer.WriterIndex / 2;
            Assert.Equal(this.testBuffer.WriterIndex / 2, stream.Position);

            // beginning
            stream.Position = 0;
            Assert.Equal(0, stream.Position);
        }

        [Fact]
        public void CannotSetThePositionPastTheEndOfTheBuffer()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);

            Assert.Throws<IndexOutOfRangeException>(() => stream.Position = this.testBuffer.WriterIndex + 1);
        }

        [Fact]
        public void CannotSetThePositionOutsideTheBoundsOfPositiveInt32()
        {
            var streamBuffer = new TestByteBuffer(int.MaxValue);
            var stream = new ReadOnlyByteBufferStream(streamBuffer, false);

            Assert.Throws<IndexOutOfRangeException>(() => stream.Position = int.MinValue);
            Assert.Throws<IndexOutOfRangeException>(() => stream.Position = -1);
            Assert.Throws<IndexOutOfRangeException>(() => stream.Position = (long)int.MaxValue + 1);
        }

        [Fact]
        public void CanSeekFromTheBeginningOfTheStream()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            int readTo = this.testBuffer.WriterIndex / 2;
            stream.Read(new byte[readTo], 0, readTo); // ensure seek calcs don't depend on read pos

            long position = stream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(0, position);
            Assert.Equal(position, stream.Position);

            position = stream.Seek(10, SeekOrigin.Begin);
            Assert.Equal(10, position);
            Assert.Equal(position, stream.Position);

            position = stream.Seek(this.testBuffer.WriterIndex, SeekOrigin.Begin);
            Assert.Equal(this.testBuffer.WriterIndex, position);
            Assert.Equal(position, stream.Position);
        }

        [Fact]
        public void CanSeekFromTheCurrentStreamPosition()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);

            long position = stream.Seek(0, SeekOrigin.Current);
            Assert.Equal(0, position);
            Assert.Equal(position, stream.Position);

            position = stream.Seek(10, SeekOrigin.Current);
            Assert.Equal(10, position);
            Assert.Equal(position, stream.Position);

            int relativeEnd = this.testBuffer.WriterIndex - 10;
            position = stream.Seek(relativeEnd, SeekOrigin.Current);
            Assert.Equal(this.testBuffer.WriterIndex, position);
            Assert.Equal(position, stream.Position);
        }

        [Fact]
        public void CanSeekFromTheEndOfTheStream()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            int readTo = this.testBuffer.WriterIndex / 2;
            stream.Read(new byte[readTo], 0, readTo); // ensure seek calcs don't depend on read pos

            long position = stream.Seek(-this.testBuffer.WriterIndex, SeekOrigin.End);
            Assert.Equal(0, position);
            Assert.Equal(position, stream.Position);

            position = stream.Seek(-10, SeekOrigin.End);
            Assert.Equal(this.testBuffer.WriterIndex - 10, position);
            Assert.Equal(position, stream.Position);

            position = stream.Seek(0, SeekOrigin.End);
            Assert.Equal(this.testBuffer.WriterIndex, position);
            Assert.Equal(position, stream.Position);
        }

        [Fact]
        public void CannotSeekOutsideTheBufferBounds()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            int length = this.testBuffer.WriterIndex;
            int half = length / 2;
            stream.Position = half;

            // before the beginning of the buffer
            Assert.Throws<IndexOutOfRangeException>(() => stream.Seek(-1, SeekOrigin.Begin));
            Assert.Throws<IndexOutOfRangeException>(() => stream.Seek(-half - 1, SeekOrigin.Current));
            Assert.Throws<IndexOutOfRangeException>(() => stream.Seek(-length - 1, SeekOrigin.End));

            // after the end of the buffer
            Assert.Throws<IndexOutOfRangeException>(() => stream.Seek(length + 1, SeekOrigin.Begin));
            Assert.Throws<IndexOutOfRangeException>(() => stream.Seek(half + 1, SeekOrigin.Current));
            Assert.Throws<IndexOutOfRangeException>(() => stream.Seek(1, SeekOrigin.End));
        }
    }
}