// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Common.Utilities;
    using Xunit;

    public abstract class AbstractCompositeByteBufferTests : AbstractByteBufferTests
    {
        protected override IByteBuffer NewBuffer(int length, int maxCapacity)
        {
            this.AssumedMaxCapacity = maxCapacity == int.MaxValue;

            var buffers = new List<IByteBuffer>();
            for (int i = 0; i < length + 45; i += 45)
            {
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[1]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[2]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[3]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[4]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[5]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[6]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[7]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[8]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[9]));
                buffers.Add(Unpooled.Empty);
            }

            IByteBuffer buffer = Unpooled.WrappedBuffer(int.MaxValue, buffers.ToArray());

            // Truncate to the requested capacity.
            buffer.AdjustCapacity(length);

            Assert.Equal(length, buffer.Capacity);
            Assert.Equal(length, buffer.ReadableBytes);
            Assert.False(buffer.IsWritable());
            buffer.SetWriterIndex(0);
            return buffer;
        }

        protected override bool DiscardReadBytesDoesNotMoveWritableBytes() => false;

        [Fact]
        public void ComponentAtOffset()
        {
            var buf = (CompositeByteBuffer)Unpooled.WrappedBuffer(
                new byte[] { 1, 2, 3, 4, 5 },
                new byte[] { 4, 5, 6, 7, 8, 9, 26 });

            //Ensure that a random place will be fine
            Assert.Equal(5, buf.ComponentAtOffset(2).Capacity);

            //Loop through each byte

            byte index = 0;
            while (index < buf.Capacity)
            {
                IByteBuffer byteBuf = buf.ComponentAtOffset(index++);
                Assert.NotNull(byteBuf);
                Assert.True(byteBuf.Capacity > 0);
                Assert.True(byteBuf.GetByte(0) > 0);
                Assert.True(byteBuf.GetByte(byteBuf.ReadableBytes - 1) > 0);
            }

            buf.Release();
        }

        [Fact]
        public void DiscardReadBytes3()
        {
            IByteBuffer a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            IByteBuffer b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 5),
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 5));
            a.SkipBytes(6);
            a.MarkReaderIndex();
            b.SkipBytes(6);
            b.MarkReaderIndex();
            Assert.Equal(a.ReaderIndex, b.ReaderIndex);
            a.SetReaderIndex(a.ReaderIndex - 1);
            b.SetReaderIndex(b.ReaderIndex - 1);
            Assert.Equal(a.ReaderIndex, b.ReaderIndex);
            a.SetWriterIndex(a.WriterIndex - 1);
            a.MarkWriterIndex();
            b.SetWriterIndex(b.WriterIndex - 1);
            b.MarkWriterIndex();
            Assert.Equal(a.WriterIndex, b.WriterIndex);
            a.SetWriterIndex(a.WriterIndex + 1);
            b.SetWriterIndex(b.WriterIndex + 1);
            Assert.Equal(a.WriterIndex, b.WriterIndex);
            Assert.True(ByteBufferUtil.Equals(a, b));
            // now discard
            a.DiscardReadBytes();
            b.DiscardReadBytes();
            Assert.Equal(a.ReaderIndex, b.ReaderIndex);
            Assert.Equal(a.WriterIndex, b.WriterIndex);
            Assert.True(ByteBufferUtil.Equals(a, b));
            a.ResetReaderIndex();
            b.ResetReaderIndex();
            Assert.Equal(a.ReaderIndex, b.ReaderIndex);
            a.ResetWriterIndex();
            b.ResetWriterIndex();
            Assert.Equal(a.WriterIndex, b.WriterIndex);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
        }

        [Fact]
        public void AutoConsolidation()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer(2);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1 }));
            Assert.Equal(1, buf.NumComponents);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 2, 3 }));
            Assert.Equal(2, buf.NumComponents);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 }));

            Assert.Equal(1, buf.NumComponents);
            Assert.True(buf.HasArray);
            Assert.NotNull(buf.Array);
            Assert.Equal(0, buf.ArrayOffset);

            buf.Release();
        }

        [Fact]
        public void CompositeToSingleBuffer()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer(3);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            Assert.Equal(1, buf.NumComponents);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 4 }));
            Assert.Equal(2, buf.NumComponents);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 5, 6 }));
            Assert.Equal(3, buf.NumComponents);

            // NOTE: hard-coding 6 here, since it seems like addComponent doesn't bump the writer index.
            // I'm unsure as to whether or not this is correct behavior
            ArraySegment<byte> nioBuffer = buf.GetIoBuffer(0, 6);
            Assert.Equal(6, nioBuffer.Count);
            Assert.True(nioBuffer.Array.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6 }));

            buf.Release();
        }

        [Fact]
        public void FullConsolidation()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer(int.MaxValue);
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 2, 3 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 }));
            buf.Consolidate();

            Assert.Equal(1, buf.NumComponents);
            Assert.True(buf.HasArray);
            Assert.NotNull(buf.Array);
            Assert.Equal(0, buf.ArrayOffset);

            buf.Release();
        }

        [Fact]
        public void RangedConsolidation()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer(int.MaxValue);
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 2, 3 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 7, 8, 9, 10 }));
            buf.Consolidate(1, 2);

            Assert.Equal(3, buf.NumComponents);
            Assert.Equal(Unpooled.WrappedBuffer(new byte[] { 1 }), buf[0]);
            Assert.Equal(Unpooled.WrappedBuffer(new byte[] { 2, 3, 4, 5, 6 }), buf[1]);
            Assert.Equal(Unpooled.WrappedBuffer(new byte[] { 7, 8, 9, 10 }), buf[2]);

            buf.Release();
        }

        [Fact]
        public void CompositeWrappedBuffer()
        {
            IByteBuffer header = Unpooled.Buffer(12);
            IByteBuffer payload = Unpooled.Buffer(512);

            header.WriteBytes(new byte[12]);
            payload.WriteBytes(new byte[512]);

            IByteBuffer buffer = Unpooled.WrappedBuffer(header, payload);

            Assert.Equal(12, header.ReadableBytes);
            Assert.Equal(512, payload.ReadableBytes);

            Assert.Equal(12 + 512, buffer.ReadableBytes);
            Assert.Equal(2, buffer.IoBufferCount);

            buffer.Release();
        }

        [Fact]
        public void SeveralBuffersEquals()
        {
            // XXX Same tests with several buffers in wrappedCheckedBuffer
            // Different length.
            IByteBuffer a = Unpooled.WrappedBuffer(new byte[] { 1 });
            IByteBuffer b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1 }),
                Unpooled.WrappedBuffer(new byte[] { 2 }));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Same content, same firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1 }),
                Unpooled.WrappedBuffer(new byte[] { 2 }),
                Unpooled.WrappedBuffer(new byte[] { 3 }));
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Same content, different firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 1, 2),
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 3, 1));
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Different content, same firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1, 2 }),
                Unpooled.WrappedBuffer(new byte[] { 4 }));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Different content, different firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 1, 2),
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 3, 1));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Same content, same firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }),
                Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 }),
                Unpooled.WrappedBuffer(new byte[] { 7, 8, 9, 10 }));
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Same content, different firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 1, 5),
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 6, 5));
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Different content, same firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 6 }),
                Unpooled.WrappedBuffer(new byte[] { 7, 8, 5, 9, 10 }));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Different content, different firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 1, 5),
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 6, 5));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
        }

        [Fact]
        public void WrappedBuffer()
        {
            var bytes = new byte[16];
            IByteBuffer a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(bytes));
            Assert.Equal(16, a.Capacity);
            a.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            IByteBuffer b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[][] { new byte[] { 1, 2, 3 } }));
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(
                new byte[] { 1 },
                new byte[] { 2 },
                new byte[] { 3 }));
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(new [] {
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }) 
            });
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1 }),
                Unpooled.WrappedBuffer(new byte[] { 2 }),
                Unpooled.WrappedBuffer(new byte[] { 3 }));
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new [] {
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 })
            }));
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1 }),
                Unpooled.WrappedBuffer(new byte[] { 2 }),
                Unpooled.WrappedBuffer(new byte[] { 3 })));
            Assert.Equal(a, b);

            a.Release();
            b.Release();
        }

        [Fact]
        public void WrittenBuffersEquals()
        {
            //XXX Same tests than testEquals with written AggregateChannelBuffers
            // Different length.
            IByteBuffer a = Unpooled.WrappedBuffer(new byte[] { 1 });
            IByteBuffer b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1 }, new byte[1]));
            IByteBuffer c = Unpooled.WrappedBuffer(new byte[] { 2 });

            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 1);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Same content, same firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1 }, new byte[2]));
            c = Unpooled.WrappedBuffer(new byte[] { 2 });

            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 2);
            b.WriteBytes(c);
            c.Release();
            c = Unpooled.WrappedBuffer(new byte[] { 3 });

            b.WriteBytes(c);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Same content, different firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 1, 3));
            c = Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 3, 1);
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 1);
            b.WriteBytes(c);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Different content, same firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2 }, new byte[1]));
            c = Unpooled.WrappedBuffer(new byte[] { 4 });
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 1);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Different content, different firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 1, 3));
            c = Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 3, 1);
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 1);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Same content, same firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }, new byte[7]));
            c = Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 });

            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 7);
            b.WriteBytes(c);
            c.Release();
            c = Unpooled.WrappedBuffer(new byte[] { 7, 8, 9, 10 });
            b.WriteBytes(c);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Same content, different firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 1, 10));
            c = Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 6, 5);
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 5);
            b.WriteBytes(c);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Different content, same firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 6 }, new byte[5]));
            c = Unpooled.WrappedBuffer(new byte[] { 7, 8, 5, 9, 10 });
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 5);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Different content, different firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 1, 10));
            c = Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 6, 5);
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 5);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();
        }

        [Fact]
        public void EmptyBuffer()
        {
            IByteBuffer b = Unpooled.WrappedBuffer(new byte[] { 1, 2 }, new byte[] { 3, 4 });
            b.ReadBytes(new byte[4]);
            b.ReadBytes(ArrayExtensions.ZeroBytes);
            b.Release();
        }

        [Fact]
        public void ReadWithEmptyCompositeBuffer()
        {
            IByteBuffer buf = Unpooled.CompositeBuffer();
            int n = 65;
            for (int i = 0; i < n; i++)
            {
                buf.WriteByte(1);
                Assert.Equal(1, buf.ReadByte());
            }
            buf.Release();
        }

        [Fact]
        public void ComponentMustBeDuplicate()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            buf.AddComponent(Unpooled.Buffer(4, 6).SetIndex(1, 3));
            Assert.IsAssignableFrom<AbstractDerivedByteBuffer>(buf[0]);
            Assert.Equal(4, buf[0].Capacity);
            Assert.Equal(6, buf[0].MaxCapacity);
            Assert.Equal(2, buf[0].ReadableBytes);
            buf.Release();
        }

        [Fact]
        public void ReferenceCounts1()
        {
            IByteBuffer c1 = Unpooled.Buffer().WriteByte(1);
            var c2 = (IByteBuffer)Unpooled.Buffer().WriteByte(2).Retain();
            var c3 = (IByteBuffer)Unpooled.Buffer().WriteByte(3).Retain(2);

            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.Equal(1, buf.ReferenceCount);
            buf.AddComponents(c1, c2, c3);

            Assert.Equal(1, buf.ReferenceCount);

            // Ensure that c[123]'s refCount did not change.
            Assert.Equal(1, c1.ReferenceCount);
            Assert.Equal(2, c2.ReferenceCount);
            Assert.Equal(3, c3.ReferenceCount);

            Assert.Equal(1, buf[0].ReferenceCount);
            Assert.Equal(2, buf[1].ReferenceCount);
            Assert.Equal(3, buf[2].ReferenceCount);

            c3.Release(2);
            c2.Release();
            buf.Release();
        }

        [Fact]
        public void ReferenceCounts2()
        {
            IByteBuffer c1 = Unpooled.Buffer().WriteByte(1);
            var c2 = (IByteBuffer)Unpooled.Buffer().WriteByte(2).Retain();
            var c3 = (IByteBuffer)Unpooled.Buffer().WriteByte(3).Retain(2);

            CompositeByteBuffer bufA = Unpooled.CompositeBuffer();
            bufA.AddComponents(c1, c2, c3).SetWriterIndex(3);

            CompositeByteBuffer bufB = Unpooled.CompositeBuffer();
            bufB.AddComponents((IByteBuffer)bufA);

            // Ensure that bufA.refCnt() did not change.
            Assert.Equal(1, bufA.ReferenceCount);

            // Ensure that c[123]'s refCnt did not change.
            Assert.Equal(1, c1.ReferenceCount);
            Assert.Equal(2, c2.ReferenceCount);
            Assert.Equal(3, c3.ReferenceCount);

            // This should decrease bufA.refCnt().
            bufB.Release();
            Assert.Equal(0, bufB.ReferenceCount);

            // Ensure bufA.refCnt() changed.
            Assert.Equal(0, bufA.ReferenceCount);

            // Ensure that c[123]'s refCnt also changed due to the deallocation of bufA.
            Assert.Equal(0, c1.ReferenceCount);
            Assert.Equal(1, c2.ReferenceCount);
            Assert.Equal(2, c3.ReferenceCount);

            c3.Release(2);
            c2.Release();
        }

        [Fact]
        public void ReferenceCounts3()
        {
            IByteBuffer c1 = Unpooled.Buffer().WriteByte(1);
            var c2 = (IByteBuffer)Unpooled.Buffer().WriteByte(2).Retain();
            var c3 = (IByteBuffer)Unpooled.Buffer().WriteByte(3).Retain(2);

            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.Equal(1, buf.ReferenceCount);

            var components = new List<IByteBuffer>
            {
                c1,
                c2,
                c3
            };
            buf.AddComponents(components);

            // Ensure that c[123]'s refCount did not change.
            Assert.Equal(1, c1.ReferenceCount);
            Assert.Equal(2, c2.ReferenceCount);
            Assert.Equal(3, c3.ReferenceCount);

            Assert.Equal(1, buf[0].ReferenceCount);
            Assert.Equal(2, buf[1].ReferenceCount);
            Assert.Equal(3, buf[2].ReferenceCount);

            c3.Release(2);
            c2.Release();
            buf.Release();
        }

        [Fact]
        public void NestedLayout()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            buf.AddComponent(
                Unpooled.CompositeBuffer()
                    .AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }))
                    .AddComponent(Unpooled.WrappedBuffer(new byte[] { 3, 4 })).Slice(1, 2));

            ArraySegment<byte>[] nioBuffers = buf.GetIoBuffers(0, 2);
            Assert.Equal(2, nioBuffers.Length);
            Assert.Equal((byte)2, nioBuffers[0].Array[nioBuffers[0].Offset]);
            Assert.Equal((byte)3, nioBuffers[1].Array[nioBuffers[1].Offset]);
            buf.Release();
        }

        [Fact]
        public void RemoveLastComponent()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
            Assert.Equal(1, buf.NumComponents);
            buf.RemoveComponent(0);
            Assert.Equal(0, buf.NumComponents);
            buf.Release();
        }

        [Fact]
        public void CopyEmpty()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.Equal(0, buf.NumComponents);

            IByteBuffer copy = buf.Copy();
            Assert.Equal(0, copy.ReadableBytes);

            buf.Release();
            copy.Release();
        }

        [Fact]
        public void DuplicateEmpty()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.Equal(0, buf.NumComponents);
            Assert.Equal(0, buf.Duplicate().ReadableBytes);

            buf.Release();
        }

        [Fact]
        public void RemoveLastComponentWithOthersLeft()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
            Assert.Equal(2, buf.NumComponents);
            buf.RemoveComponent(1);
            Assert.Equal(1, buf.NumComponents);
            buf.Release();
        }

        [Fact]
        public void DiscardSomeReadBytes()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            int len = 8 * 4;
            for (int i = 0; i < len; i += 4)
            {
                IByteBuffer buf = Unpooled.Buffer().WriteInt(i);
                cbuf.AdjustCapacity(cbuf.WriterIndex);
                cbuf.AddComponent(buf).SetWriterIndex(i + 4);
            }
            cbuf.WriteByte(1);

            var me = new byte[len];
            cbuf.ReadBytes(me);
            cbuf.ReadByte();

            cbuf.DiscardSomeReadBytes();
            cbuf.Release();
        }

        [Fact]
        public void AddEmptyBufferRelease()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            IByteBuffer buf = Unpooled.Buffer();
            Assert.Equal(1, buf.ReferenceCount);
            cbuf.AddComponent(buf);
            Assert.Equal(1, buf.ReferenceCount);

            cbuf.Release();
            Assert.Equal(0, buf.ReferenceCount);
        }

        [Fact]
        public void AddEmptyBuffersRelease()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            IByteBuffer buf = Unpooled.Buffer();
            IByteBuffer buf2 = Unpooled.Buffer().WriteInt(1);
            IByteBuffer buf3 = Unpooled.Buffer();

            Assert.Equal(1, buf.ReferenceCount);
            Assert.Equal(1, buf2.ReferenceCount);
            Assert.Equal(1, buf3.ReferenceCount);

            cbuf.AddComponents(buf, buf2, buf3);
            Assert.Equal(1, buf.ReferenceCount);
            Assert.Equal(1, buf2.ReferenceCount);
            Assert.Equal(1, buf3.ReferenceCount);

            cbuf.Release();
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, buf2.ReferenceCount);
            Assert.Equal(0, buf3.ReferenceCount);
        }

        [Fact]
        public void AddEmptyBufferInMiddle()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            IByteBuffer buf1 = Unpooled.Buffer().WriteByte(1);
            cbuf.AddComponent(true, buf1);
            cbuf.AddComponent(true, Unpooled.Empty);
            IByteBuffer buf3 = Unpooled.Buffer().WriteByte(2);
            cbuf.AddComponent(true, buf3);

            Assert.Equal(2, cbuf.ReadableBytes);
            Assert.Equal((byte)1, cbuf.ReadByte());
            Assert.Equal((byte)2, cbuf.ReadByte());

            Assert.Same(Unpooled.Empty, cbuf.InternalComponent(1));
            Assert.NotSame(Unpooled.Empty, cbuf.InternalComponentAtOffset(1));
            cbuf.Release();
        }

        [Fact]
        public void ReleasesItsComponents()
        {
            IByteBuffer buffer = PooledByteBufferAllocator.Default.Buffer(); // 1

            buffer.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

            var s1 = (IByteBuffer)buffer.ReadSlice(2).Retain(); // 2
            var s2 = (IByteBuffer)s1.ReadSlice(2).Retain(); // 3
            var s3 = (IByteBuffer)s2.ReadSlice(2).Retain(); // 4
            var s4 = (IByteBuffer)s3.ReadSlice(2).Retain(); // 5

            IByteBuffer composite = PooledByteBufferAllocator.Default.CompositeBuffer()
                .AddComponent(s1)
                .AddComponents(s2, s3, s4);

            Assert.Equal(1, composite.ReferenceCount);
            Assert.Equal(5, buffer.ReferenceCount);

            // releasing composite should release the 4 components
            ReferenceCountUtil.Release(composite);
            Assert.Equal(0, composite.ReferenceCount);
            Assert.Equal(1, buffer.ReferenceCount);

            // last remaining ref to buffer
            ReferenceCountUtil.Release(buffer);
            Assert.Equal(0, buffer.ReferenceCount);
        }

        [Fact]
        public void AllocatorIsSameWhenCopy() => this.AllocatorIsSameWhenCopy0(false);

        [Fact]
        public void AllocatorIsSameWhenCopyUsingIndexAndLength() => this.AllocatorIsSameWhenCopy0(true);

        void AllocatorIsSameWhenCopy0(bool withIndexAndLength)
        {
            IByteBuffer buffer = this.NewBuffer(8);
            buffer.WriteZero(4);
            IByteBuffer copy = withIndexAndLength ? buffer.Copy(0, 4) : buffer.Copy();
            Assert.Equal(buffer, copy);
            Assert.Same(buffer.Allocator, copy.Allocator);
            buffer.Release();
            copy.Release();
        }
    }
}
