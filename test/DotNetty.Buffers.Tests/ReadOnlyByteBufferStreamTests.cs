// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.IO;
    using DotNetty.Common.Utilities;
    using Moq;
    using Xunit;

    public class ReadOnlyByteBufferStreamTests
    {
        IByteBuffer testBuffer;

        void SetupByteBuffer(int length)
        {
            int reader = 0;
            int writer = length;
            var mock = new Mock<IByteBuffer>();

            mock.Setup(buf => buf.ReadableBytes).Returns(() => writer - reader);
            mock.Setup(buf => buf.ReaderIndex).Returns(() => reader);
            mock.Setup(buf => buf.WriterIndex).Returns(() => writer);

            mock.Setup(buf => buf.ReadBytes(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback((byte[] buffer, int offset, int count) =>
                {
                    buffer.Fill(offset, count, (byte)42);
                    reader += count;
                })
                .Returns(mock.Object);

            mock.Setup(buf => buf.SetReaderIndex(It.IsAny<int>()))
                .Callback((int index) =>
                {
                    if (index < 0 || index > writer)
                    {
                        throw new IndexOutOfRangeException();
                    }

                    reader = index;
                })
                .Returns(mock.Object);

            this.testBuffer = mock.Object;
        }

        public ReadOnlyByteBufferStreamTests()
        {
            SetupByteBuffer(4);
        }

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
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];
            stream.Read(output, 0, 2);
            int read = stream.Read(output, 2, 2);

            Assert.Equal(2, read);
            Assert.Equal(new byte[] { 42, 42, 42, 42 }, output);
        }

        [Fact]
        public void SingleReadCannotPassTheEndOfTheStream()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[6];

            // single read is too big for the stream
            stream.Read(output, 0, 6);

            Assert.Equal(new byte[] { 42, 42, 42, 42, 0, 0 }, output);
        }

        [Fact]
        public void MultiReadCannotPassTheEndOfTheStream()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[6];

            // 2nd read is too big for the stream
            stream.Read(output, 0, 2);
            stream.Read(output, 0, 4);

            Assert.Equal(new byte[] { 42, 42, 0, 0, 0, 0 }, output);
        }

        [Fact]
        public void SingleReadCannotWritePastTheEndOfTheDestinationBuffer()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[3];

            // single read is too big for the output buffer
            var e = Assert.Throws<ArgumentException>(() => stream.Read(output, 0, 4));
            Assert.Equal("The sum of offset and count is larger than the output length", e.Message);
        }

        [Fact]
        public void MultiReadCannotWritePastTheEndOfTheDestinationBuffer()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[3];

            // 2nd read is too big for the output buffer
            stream.Read(output, 0, 2);
            Assert.Throws<ArgumentException>(() => stream.Read(output, 2, 2));
        }

        [Fact]
        public void ReadZeroBytesFromTheEndOfTheStream()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
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
            Assert.Equal(this.testBuffer.WriterIndex, stream.Length);

            stream.Read(output, 0, 4);
            Assert.Equal(this.testBuffer.WriterIndex, stream.Length);
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
            SetupByteBuffer(int.MaxValue);
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);

            Assert.Throws<IndexOutOfRangeException>(() => stream.Position = int.MinValue);
            Assert.Throws<IndexOutOfRangeException>(() => stream.Position = -1);
            Assert.Throws<IndexOutOfRangeException>(() => stream.Position = (long)int.MaxValue + 1);
        }

        [Fact]
        public void CanSeekFromTheBeginningOfTheStream()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            stream.Position = this.testBuffer.WriterIndex / 2; // ensure seek calcs don't depend on read pos

            long position = stream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(0, position);
            Assert.Equal(position, stream.Position);

            position = stream.Seek(2, SeekOrigin.Begin);
            Assert.Equal(2, position);
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

            position = stream.Seek(2, SeekOrigin.Current);
            Assert.Equal(2, position);
            Assert.Equal(position, stream.Position);

            int relativeEnd = this.testBuffer.WriterIndex - (int)position;
            position = stream.Seek(relativeEnd, SeekOrigin.Current);
            Assert.Equal(this.testBuffer.WriterIndex, position);
            Assert.Equal(position, stream.Position);
        }

        [Fact]
        public void CanSeekFromTheEndOfTheStream()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            int readTo = this.testBuffer.WriterIndex / 2;
            stream.Position = this.testBuffer.WriterIndex / 2; // ensure seek calcs don't depend on read pos

            long position = stream.Seek(-this.testBuffer.WriterIndex, SeekOrigin.End);
            Assert.Equal(0, position);
            Assert.Equal(position, stream.Position);

            position = stream.Seek(-2, SeekOrigin.End);
            Assert.Equal(this.testBuffer.WriterIndex - 2, position);
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