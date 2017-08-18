// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;
    using static Unpooled;

    /// <summary>
    /// Tests channel buffers
    /// </summary>
    public class UnpooledTest
    {
        static readonly IByteBuffer[] EmptyByteBuffer = new IByteBuffer[0];
        static readonly byte[][] EmptyBytes2D = new byte[0][];
        static readonly byte[] EmptyBytes = { };

        [Fact]
        public void TestCompositeWrappedBuffer()
        {
            IByteBuffer header = Buffer(12);
            IByteBuffer payload = Buffer(512);

            header.WriteBytes(new byte[12]);
            payload.WriteBytes(new byte[512]);

            IByteBuffer buffer = WrappedBuffer(header, payload);

            Assert.Equal(12, header.ReadableBytes);
            Assert.Equal(512, payload.ReadableBytes);

            Assert.Equal(12 + 512, buffer.ReadableBytes);
            Assert.Equal(2, buffer.IoBufferCount);

            buffer.Release();
        }

        [Fact]
        public void TestHashCode()
        {
            var map = new Dictionary<byte[], int>();
            map.Add(EmptyBytes, 1);
            map.Add(new byte[] { 1 }, 32);
            map.Add(new byte[] { 2 }, 33);
            map.Add(new byte[] { 0, 1 }, 962);
            map.Add(new byte[] { 1, 2 }, 994);
            map.Add(new byte[] { 0, 1, 2, 3, 4, 5 }, 63504931);
            map.Add(new byte[] { 6, 7, 8, 9, 0, 1 }, unchecked((int)97180294697L));

            foreach (KeyValuePair<byte[], int> e in map)
            {
                IByteBuffer buffer = WrappedBuffer(e.Key);
                Assert.Equal(e.Value, ByteBufferUtil.HashCode(buffer));
                buffer.Release();
            }
        }

        [Fact]
        public void TestEquals()
        {
            IByteBuffer a, b;

            // Different length.
            a = WrappedBuffer(new byte[] { 1 });
            b = WrappedBuffer(new byte[] { 1, 2 });
            Assert.False(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();

            // Same content, same firstIndex, short length.
            a = WrappedBuffer(new byte[] { 1, 2, 3 });
            b = WrappedBuffer(new byte[] { 1, 2, 3 });
            Assert.True(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();

            // Same content, different firstIndex, short length.
            a = WrappedBuffer(new byte[] { 1, 2, 3 });
            b = WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 1, 3);
            Assert.True(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();

            // Different content, same firstIndex, short length.
            a = WrappedBuffer(new byte[] { 1, 2, 3 });
            b = WrappedBuffer(new byte[] { 1, 2, 4 });
            Assert.False(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();

            // Different content, different firstIndex, short length.
            a = WrappedBuffer(new byte[] { 1, 2, 3 });
            b = WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 1, 3);
            Assert.False(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();

            // Same content, same firstIndex, long length.
            a = WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            Assert.True(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();

            // Same content, different firstIndex, long length.
            a = WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 1, 10);
            Assert.True(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();

            // Different content, same firstIndex, long length.
            a = WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = WrappedBuffer(new byte[] { 1, 2, 3, 4, 6, 7, 8, 5, 9, 10 });
            Assert.False(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();

            // Different content, different firstIndex, long length.
            a = WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 1, 10);
            Assert.False(ByteBufferUtil.Equals(a, b));
            a.Release();
            b.Release();
        }

        [Fact]
        public void TestCompare()
        {
            IList<IByteBuffer> expected = new List<IByteBuffer>();
            expected.Add(WrappedBuffer(new byte[] { 1 }));
            expected.Add(WrappedBuffer(new byte[] { 1, 2 }));
            expected.Add(WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
            expected.Add(WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }));
            expected.Add(WrappedBuffer(new byte[] { 2 }));
            expected.Add(WrappedBuffer(new byte[] { 2, 3 }));
            expected.Add(WrappedBuffer(new byte[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }));
            expected.Add(WrappedBuffer(new byte[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }));
            expected.Add(WrappedBuffer(new byte[] { 2, 3, 4 }, 1, 1));
            expected.Add(WrappedBuffer(new byte[] { 1, 2, 3, 4 }, 2, 2));
            expected.Add(WrappedBuffer(new byte[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }, 1, 10));
            expected.Add(WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }, 2, 12));
            expected.Add(WrappedBuffer(new byte[] { 2, 3, 4, 5 }, 2, 1));
            expected.Add(WrappedBuffer(new byte[] { 1, 2, 3, 4, 5 }, 3, 2));
            expected.Add(WrappedBuffer(new byte[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }, 2, 10));
            expected.Add(WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, 3, 12));

            for (int i = 0; i < expected.Count; i++)
            {
                for (int j = 0; j < expected.Count; j++)
                {
                    if (i == j)
                    {
                        Assert.Equal(0, ByteBufferUtil.Compare(expected[i], expected[j]));
                    }
                    else if (i < j)
                    {
                        Assert.True(ByteBufferUtil.Compare(expected[i], expected[j]) < 0);
                    }
                    else
                    {
                        Assert.True(ByteBufferUtil.Compare(expected[i], expected[j]) > 0);
                    }
                }
            }
            foreach (IByteBuffer buffer in expected)
            {
                buffer.Release();
            }
        }

        [Fact]
        public void ShouldReturnEmptyBufferWhenLengthIsZero()
        {
            AssertSameAndRelease(Empty, WrappedBuffer(EmptyBytes));
            AssertSameAndRelease(Empty, WrappedBuffer(new byte[8], 0, 0));
            AssertSameAndRelease(Empty, WrappedBuffer(new byte[8], 8, 0));
            AssertSameAndRelease(Empty, WrappedBuffer(Empty));
            AssertSameAndRelease(Empty, WrappedBuffer(EmptyBytes2D));
            AssertSameAndRelease(Empty, WrappedBuffer(new[] { EmptyBytes }));
            AssertSameAndRelease(Empty, WrappedBuffer(EmptyByteBuffer));
            AssertSameAndRelease(Empty, WrappedBuffer(new IByteBuffer[] { Buffer(0) }));
            AssertSameAndRelease(Empty, WrappedBuffer(Buffer(0), Buffer(0)));
            AssertSameAndRelease(Empty, CopiedBuffer(EmptyBytes));
            AssertSameAndRelease(Empty, CopiedBuffer(new byte[8], 0, 0));
            AssertSameAndRelease(Empty, CopiedBuffer(new byte[8], 8, 0));
            AssertSameAndRelease(Empty, CopiedBuffer(Empty));
            Assert.Same(Empty, CopiedBuffer(EmptyBytes2D));
            AssertSameAndRelease(Empty, CopiedBuffer(new[] { EmptyBytes }));
            AssertSameAndRelease(Empty, CopiedBuffer(EmptyByteBuffer));
            AssertSameAndRelease(Empty, CopiedBuffer(new IByteBuffer[] { Buffer(0) }));
            AssertSameAndRelease(Empty, CopiedBuffer(Buffer(0), Buffer(0)));
        }

        static void AssertSameAndRelease(IByteBuffer expected, IByteBuffer actual)
        {
            Assert.Same(expected, actual);
            expected.Release();
            actual.Release();
        }

        static void AssertEqualAndRelease(IByteBuffer expected, IByteBuffer actual)
        {
            Assert.Equal(expected, actual);
            expected.Release();
            actual.Release();
        }

        [Fact]
        public void TestCompare2()
        {
            IByteBuffer expected = WrappedBuffer(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
            IByteBuffer actual = WrappedBuffer(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            Assert.True(ByteBufferUtil.Compare(expected, actual) > 0);
            expected.Release();
            actual.Release();

            expected = WrappedBuffer(new byte[] { 0xFF });
            actual = WrappedBuffer(new byte[] { 0x00 });
            Assert.True(ByteBufferUtil.Compare(expected, actual) > 0);
            expected.Release();
            actual.Release();
        }

        [Fact]
        public void ShouldAllowEmptyBufferToCreateCompositeBuffer()
        {
            IByteBuffer buf = WrappedBuffer(Empty, WrappedBuffer(new byte[16]).WithOrder(ByteOrder.LittleEndian), Empty);
            try
            {
                Assert.Equal(16, buf.Capacity);
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void TestWrappedBuffer()
        {
            AssertEqualAndRelease(
                WrappedBuffer(new byte[] { 1, 2, 3 }),
                WrappedBuffer(new byte[][] { new byte[] { 1, 2, 3 } }));

            AssertEqualAndRelease(
                WrappedBuffer(new byte[] { 1, 2, 3 }),
                WrappedBuffer(new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }));

            AssertEqualAndRelease(WrappedBuffer(new byte[] { 1, 2, 3 }),
                WrappedBuffer(new IByteBuffer[] { WrappedBuffer(new byte[] { 1, 2, 3 }) }));

            AssertEqualAndRelease(
                WrappedBuffer(new byte[] { 1, 2, 3 }),
                WrappedBuffer(WrappedBuffer(new byte[] { 1 }),
                    WrappedBuffer(new byte[] { 2 }), WrappedBuffer(new byte[] { 3 })));
        }

        [Fact]
        public void TestSingleWrappedByteBufReleased()
        {
            IByteBuffer buf = Buffer(12).WriteByte(0);
            IByteBuffer wrapped = WrappedBuffer(buf);
            Assert.True(wrapped.Release());
            Assert.Equal(0, buf.ReferenceCount);
        }

        [Fact]
        public void TestSingleUnReadableWrappedByteBufReleased()
        {
            IByteBuffer buf = Buffer(12);
            IByteBuffer wrapped = WrappedBuffer(buf);
            Assert.False(wrapped.Release()); // Empty Buffer cannot be released
            Assert.Equal(0, buf.ReferenceCount);
        }

        [Fact]
        public void TestMultiByteBufReleased()
        {
            IByteBuffer buf1 = Buffer(12).WriteByte(0);
            IByteBuffer buf2 = Buffer(12).WriteByte(0);
            IByteBuffer wrapped = WrappedBuffer(16, buf1, buf2);
            Assert.True(wrapped.Release());
            Assert.Equal(0, buf1.ReferenceCount);
            Assert.Equal(0, buf2.ReferenceCount);
        }

        [Fact]
        public void TestMultiUnReadableByteBufReleased()
        {
            IByteBuffer buf1 = Buffer(12);
            IByteBuffer buf2 = Buffer(12);
            IByteBuffer wrapped = WrappedBuffer(16, buf1, buf2);
            Assert.False(wrapped.Release()); // Empty Buffer cannot be released
            Assert.Equal(0, buf1.ReferenceCount);
            Assert.Equal(0, buf2.ReferenceCount);
        }

        [Fact]
        public void TestCopiedBuffer()
        {
            AssertEqualAndRelease(WrappedBuffer(new byte[] { 1, 2, 3 }),
                CopiedBuffer(new byte[][] { new byte[] { 1, 2, 3 } }));

            AssertEqualAndRelease(WrappedBuffer(new byte[] { 1, 2, 3 }),
                CopiedBuffer(new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }));

            AssertEqualAndRelease(WrappedBuffer(new byte[] { 1, 2, 3 }),
                CopiedBuffer(new IByteBuffer[] { WrappedBuffer(new byte[] { 1, 2, 3 }) }));

            AssertEqualAndRelease(WrappedBuffer(new byte[] { 1, 2, 3 }),
                CopiedBuffer(WrappedBuffer(new byte[] { 1 }),
                    WrappedBuffer(new byte[] { 2 }), WrappedBuffer(new byte[] { 3 })));
        }

        [Fact]
        public void TestHexDump()
        {
            Assert.Equal("", ByteBufferUtil.HexDump(Empty));

            IByteBuffer buffer = WrappedBuffer(new byte[] { 0x12, 0x34, 0x56 });
            Assert.Equal("123456", ByteBufferUtil.HexDump(buffer));
            buffer.Release();

            buffer = WrappedBuffer(new byte[]{
                0x12, 0x34, 0x56, 0x78,
                0x90, 0xAB, 0xCD, 0xEF
            });
            Assert.Equal("1234567890abcdef", ByteBufferUtil.HexDump(buffer));
            buffer.Release();
        }

        [Fact]
        public void TestSwapMedium()
        {
            Assert.Equal(0x563412, ByteBufferUtil.SwapMedium(0x123456));
            Assert.Equal(0x80, ByteBufferUtil.SwapMedium(0x800000));
        }

        [Fact]
        public void TestWrapSingleInt()
        {
            IByteBuffer buffer = CopyInt(42);
            Assert.Equal(4, buffer.Capacity);
            Assert.Equal(42, buffer.ReadInt());
            Assert.False(buffer.IsReadable());
            buffer.Release();
        }

        [Fact]
        public void TestWrapInt()
        {
            IByteBuffer buffer = CopyInt(1, 4);
            Assert.Equal(8, buffer.Capacity);
            Assert.Equal(1, buffer.ReadInt());
            Assert.Equal(4, buffer.ReadInt());
            Assert.False(buffer.IsReadable());
            buffer.Release();

            buffer = CopyInt(null);
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();

            buffer = CopyInt(new int[] { });
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();
        }

        [Fact]
        public void TestWrapSingleShort()
        {
            IByteBuffer buffer = CopyShort(42);
            Assert.Equal(2, buffer.Capacity);
            Assert.Equal(42, buffer.ReadShort());
            Assert.False(buffer.IsReadable());
            buffer.Release();
        }

        [Fact]
        public void TestWrapShortFromShortArray()
        {
            IByteBuffer buffer = CopyShort(1, 4);
            Assert.Equal(4, buffer.Capacity);
            Assert.Equal(1, buffer.ReadShort());
            Assert.Equal(4, buffer.ReadShort());
            Assert.False(buffer.IsReadable());
            buffer.Release();


            buffer = CopyShort(null);
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();

            buffer = CopyShort(new short[] { });
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();
        }

        [Fact]
        public void TestWrapSingleMedium()
        {
            IByteBuffer buffer = CopyMedium(42);
            Assert.Equal(3, buffer.Capacity);
            Assert.Equal(42, buffer.ReadMedium());
            Assert.False(buffer.IsReadable());
            buffer.Release();
        }

        [Fact]
        public void TestWrapMedium()
        {
            IByteBuffer buffer = CopyMedium(1, 4);
            Assert.Equal(6, buffer.Capacity);
            Assert.Equal(1, buffer.ReadMedium());
            Assert.Equal(4, buffer.ReadMedium());
            Assert.False(buffer.IsReadable());
            buffer.Release();

            buffer = CopyMedium(null);
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();

            buffer = CopyMedium(new int[] { });
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();
        }

        [Fact]
        public void TestWrapSingleLong()
        {
            IByteBuffer buffer = CopyLong(42);
            Assert.Equal(8, buffer.Capacity);
            Assert.Equal(42, buffer.ReadLong());
            Assert.False(buffer.IsReadable());
            buffer.Release();
        }

        [Fact]
        public void TestWrapLong()
        {
            IByteBuffer buffer = CopyLong(1, 4);
            Assert.Equal(16, buffer.Capacity);
            Assert.Equal(1, buffer.ReadLong());
            Assert.Equal(4, buffer.ReadLong());
            Assert.False(buffer.IsReadable());
            buffer.Release();

            buffer = CopyLong(null);
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();

            buffer = CopyLong(new long[] { });
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();
        }

        [Fact]
        public void TestWrapSingleFloat()
        {
            IEqualityComparer<float> comparer = new ApproximateComparer(0.01);
            IByteBuffer buffer = CopyFloat(42);
            Assert.Equal(4, buffer.Capacity);
            Assert.Equal(42, buffer.ReadFloat(), comparer);
            Assert.False(buffer.IsReadable());
            buffer.Release();
        }

        [Fact]
        public void TestWrapFloat()
        {
            IEqualityComparer<float> comparer = new ApproximateComparer(0.01);
            IByteBuffer buffer = CopyFloat(1, 4);
            Assert.Equal(8, buffer.Capacity);
            Assert.Equal(1, buffer.ReadFloat(), comparer);
            Assert.Equal(4, buffer.ReadFloat(), comparer);
            Assert.False(buffer.IsReadable());
            buffer.Release();

            buffer = CopyFloat(null);
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();

            buffer = CopyFloat(new float[] { });
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();
        }

        [Fact]
        public void TestWrapSingleDouble()
        {
            IEqualityComparer<double> comparer = new ApproximateComparer(0.01);
            IByteBuffer buffer = CopyDouble(42);
            Assert.Equal(8, buffer.Capacity);
            Assert.Equal(42, buffer.ReadDouble(), comparer);
            Assert.False(buffer.IsReadable());
            buffer.Release();
        }

        [Fact]
        public void TestWrapDouble()
        {
            IEqualityComparer<double> comparer = new ApproximateComparer(0.01);
            IByteBuffer buffer = CopyDouble(1, 4);
            Assert.Equal(16, buffer.Capacity);
            Assert.Equal(1, buffer.ReadDouble(), comparer);
            Assert.Equal(4, buffer.ReadDouble(), comparer);
            Assert.False(buffer.IsReadable());

            buffer = CopyDouble(null);
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();

            buffer = CopyDouble(new double[] { });
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();
        }

        [Fact]
        public void TestWrapBoolean()
        {
            IByteBuffer buffer = CopyBoolean(true, false);
            Assert.Equal(2, buffer.Capacity);
            Assert.True(buffer.ReadBoolean());
            Assert.False(buffer.ReadBoolean());
            Assert.False(buffer.IsReadable());
            buffer.Release();

            buffer = CopyBoolean(null);
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();

            buffer = CopyBoolean(new bool[] { });
            Assert.Equal(0, buffer.Capacity);
            buffer.Release();
        }

        [Fact]
        public void SkipBytesNegativeLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                IByteBuffer buf = Buffer(8);
                try
                {
                    buf.SkipBytes(-1);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestInconsistentOrder()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                IByteBuffer buf = WrappedBuffer(new byte[] { 1 }).WithOrder(ByteOrder.BigEndian);
                IByteBuffer buf1 = WrappedBuffer(new byte[] { 2, 3 }).WithOrder(ByteOrder.LittleEndian);
                try
                {
                    CopiedBuffer(buf, buf1);
                }
                finally
                {
                    buf.Release();
                    buf1.Release();
                }
            });
        }

        [Fact]
        public void TestWrapByteBufArrayStartsWithNonReadable()
        {
            IByteBuffer buffer1 = Buffer(8);
            IByteBuffer buffer2 = Buffer(8).WriteZero(8); // Ensure the IByteBuffer is readable.
            IByteBuffer buffer3 = Buffer(8);
            IByteBuffer buffer4 = Buffer(8).WriteZero(8); // Ensure the IByteBuffer is readable.

            IByteBuffer wrapped = WrappedBuffer(buffer1, buffer2, buffer3, buffer4);
            Assert.Equal(16, wrapped.ReadableBytes);
            Assert.True(wrapped.Release());
            Assert.Equal(0, buffer1.ReferenceCount);
            Assert.Equal(0, buffer2.ReferenceCount);
            Assert.Equal(0, buffer3.ReferenceCount);
            Assert.Equal(0, buffer4.ReferenceCount);
            Assert.Equal(0, wrapped.ReferenceCount);
        }
    }
    public class ApproximateComparer : IEqualityComparer<double>, IEqualityComparer<float>
    {
        public double MarginOfError { get; }

        public ApproximateComparer(double marginOfError)
        {
            if (marginOfError <= 0 || marginOfError >= 1.0)
                throw new ArgumentOutOfRangeException($"{nameof(marginOfError)} must be not less than 0 and not greater than 1");

            this.MarginOfError = marginOfError;
        }

        public bool Equals(double x, double y)
        {
            return Math.Abs(x - y) <= this.MarginOfError;
        }

        public bool Equals(float x, float y)
        {
            return Math.Abs(x - y) <= (float)this.MarginOfError;
        }

        public int GetHashCode(double obj)
        {
            return obj.GetHashCode();
        }

        public int GetHashCode(float obj)
        {
            return obj.GetHashCode();
        }
    }
}
