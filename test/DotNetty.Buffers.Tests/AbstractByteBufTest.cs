// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using DotNetty.Common.Utilities;
    using Xunit;

    /**
 * An abstract test class for channel buffers
 */

    public abstract class AbstractByteBufTest : IDisposable
    {
        static readonly int Capacity = 4096; // Must be even
        static readonly int BlockSize = 128;

        readonly int seed;
        Random random;
        IByteBuffer buffer;

        protected abstract IByteBuffer NewBuffer(int capacity);

        protected virtual bool DiscardReadBytesDoesNotMoveWritableBytes() => true;

        protected AbstractByteBufTest()
        {
            this.buffer = this.NewBuffer(Capacity);
            this.seed = Environment.TickCount;
            this.random = new Random(this.seed);
        }

        public void Dispose()
        {
            if (this.buffer != null)
            {
                Assert.True(this.buffer.Release());
                Assert.Equal(0, this.buffer.ReferenceCount);

                try
                {
                    this.buffer.Release();
                }
                catch (Exception)
                {
                    // Ignore.
                }
                this.buffer = null;
            }
        }

        [Fact]
        public void InitialState()
        {
            Assert.Equal(Capacity, this.buffer.Capacity);
            Assert.Equal(0, this.buffer.ReaderIndex);
        }

        [Fact]
        public void ReaderIndexBoundaryCheck1()
        {
            this.buffer.SetWriterIndex(0);
            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetReaderIndex(-1));
        }

        [Fact]
        public void ReaderIndexBoundaryCheck2()
        {
            this.buffer.SetWriterIndex(this.buffer.Capacity);
            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetReaderIndex(this.buffer.Capacity + 1));
        }

        [Fact]
        public void ReaderIndexBoundaryCheck3()
        {
            this.buffer.SetWriterIndex(Capacity / 2);
            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetReaderIndex(Capacity * 3 / 2));
        }

        [Fact]
        public void ReaderIndexBoundaryCheck4()
        {
            this.buffer.SetWriterIndex(0);
            this.buffer.SetReaderIndex(0);
            this.buffer.SetWriterIndex(this.buffer.Capacity);
            this.buffer.SetReaderIndex(this.buffer.Capacity);
        }

        [Fact]
        public void WriterIndexBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetWriterIndex(-1));

        [Fact]
        public void WriterIndexBoundaryCheck2()
        {
            this.buffer.SetWriterIndex(Capacity);
            this.buffer.SetReaderIndex(Capacity);
            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetWriterIndex(this.buffer.Capacity + 1));
        }

        [Fact]
        public void WriterIndexBoundaryCheck3()
        {
            this.buffer.SetWriterIndex(Capacity);
            this.buffer.SetReaderIndex(Capacity / 2);
            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetWriterIndex(Capacity / 4));
        }

        [Fact]
        public void WriterIndexBoundaryCheck4()
        {
            this.buffer.SetWriterIndex(0);
            this.buffer.SetReaderIndex(0);
            this.buffer.SetWriterIndex(Capacity);

            this.buffer.WriteBytes(ArrayExtensions.ZeroBytes);
        }

        [Fact]
        public void GetBooleanBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBoolean(-1));

        [Fact]
        public void GetBooleanBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBoolean(this.buffer.Capacity));

        [Fact]
        public void GetByteBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetByte(-1));

        [Fact]
        public void GetByteBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetByte(this.buffer.Capacity));

        [Fact]
        public void GetShortBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetShort(-1));

        [Fact]
        public void GetShortBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetShort(this.buffer.Capacity - 1));

        [Fact]
        public void GetIntBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetInt(-1));

        [Fact]
        public void GetIntBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetInt(this.buffer.Capacity - 3));

        [Fact]
        public void GetLongBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetLong(-1));

        [Fact]
        public void GetMediumBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetMedium(-1));

        [Fact]
        public void GetMediumBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetMedium(this.buffer.Capacity -2));

        [Fact]
        public void GetLongBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetLong(this.buffer.Capacity - 7));

        [Fact]
        public void GetByteArrayBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBytes(-1, ArrayExtensions.ZeroBytes));

        [Fact]
        public void GetByteArrayBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBytes(-1, ArrayExtensions.ZeroBytes, 0, 0));

        [Fact]
        public void GetByteArrayBoundaryCheck3()
        {
            var dst = new byte[4];
            this.buffer.SetInt(0, 0x01020304);
            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBytes(0, dst, -1, 4));

            // No partial copy is expected.
            Assert.Equal(0, dst[0]);
            Assert.Equal(0, dst[1]);
            Assert.Equal(0, dst[2]);
            Assert.Equal(0, dst[3]);
        }

        [Fact]
        public void GetByteArrayBoundaryCheck4()
        {
            var dst = new byte[4];
            this.buffer.SetInt(0, 0x01020304);
            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBytes(0, dst, 1, 4));

            // No partial copy is expected.
            Assert.Equal(0, dst[0]);
            Assert.Equal(0, dst[1]);
            Assert.Equal(0, dst[2]);
            Assert.Equal(0, dst[3]);
        }

        [Fact]
        public void CopyBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.Copy(-1, 0));

        [Fact]
        public void CopyBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.Copy(0, this.buffer.Capacity + 1));

        [Fact]
        public void CopyBoundaryCheck3() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.Copy(this.buffer.Capacity + 1, 0));

        [Fact]
        public void CopyBoundaryCheck4() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.Copy(this.buffer.Capacity, 1));

        [Fact]
        public void SetIndexBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetIndex(-1, Capacity));

        [Fact]
        public void SetIndexBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetIndex(Capacity / 2, Capacity / 4));

        [Fact]
        public void SetIndexBoundaryCheck3() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SetIndex(0, Capacity + 1));

        [Fact]
        public void GetByteBufferState()
        {
            var dst = new byte[4];

            this.buffer.SetByte(0, 1);
            this.buffer.SetByte(1, 2);
            this.buffer.SetByte(2, 3);
            this.buffer.SetByte(3, 4);
            this.buffer.GetBytes(1, dst, 1, 2);

            Assert.Equal(0, dst[0]);
            Assert.Equal(2, dst[1]);
            Assert.Equal(3, dst[2]);
            Assert.Equal(0, dst[3]);
        }

        [Fact]
        public void GetDirectByteBufferBoundaryCheck() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetBytes(-1, new byte[0]));

        [Fact]
        public void TestRandomByteAccess()
        {
            for (int i = 0; i < this.buffer.Capacity; i ++)
            {
                byte value = (byte)this.random.Next();
                this.buffer.SetByte(i, value);
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity; i ++)
            {
                byte value = (byte)this.random.Next();
                Assert.Equal(value, this.buffer.GetByte(i));
            }
        }

        [Fact]
        public void TestRandomMediumAccess() => this.TestRandomMediumAccess(true);

        [Fact]
        public void TestRandomMediumLeAccess() => this.TestRandomMediumAccess(false);

        public void TestRandomMediumAccess(bool testBigEndian)
        {
            for (int i = 0; i < this.buffer.Capacity - 2; i += 3)
            {
                int value = this.random.Next();
                if (testBigEndian)
                {
                    this.buffer.SetMedium(i, value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).SetMedium(i, value);
                }
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 2; i += 3)
            {
                int value = this.random.Next() << 8 >> 8;
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.GetMedium(i));
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).GetMedium(i));
                }
            }
        }

        [Fact]
        public void TestRandomUnsignedMediumAccess() => this.TestRandomUnsignedMediumAccess(true);

        [Fact]
        public void TestRandomUnsignedMediumLeAccess() => this.TestRandomUnsignedMediumAccess(false);

        public void TestRandomUnsignedMediumAccess(bool testBigEndian)
        {
            for (int i = 0; i < this.buffer.Capacity - 2; i += 3)
            {
                int value = this.random.Next();
                if (testBigEndian)
                {
                    this.buffer.SetMedium(i, value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).SetMedium(i, value);
                }
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 2; i += 3)
            {
                int value = this.random.Next().ToUnsignedMediumInt();
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.GetUnsignedMedium(i));
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).GetUnsignedMedium(i));
                }
            }
        }

        [Fact]
        public void TestRandomShortAccess() => this.TestRandomShortAccess(true);

        [Fact]
        public void TestRandomShortLeAccess() => this.TestRandomShortAccess(false);

        void TestRandomShortAccess(bool testBigEndian)
        {
            for (int i = 0; i < this.buffer.Capacity - 1; i += 2)
            {
                short value = (short)this.random.Next();
                if (testBigEndian)
                {
                    this.buffer.SetShort(i, value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).SetShort(i, value);
                }
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 1; i += 2)
            {
                short value = (short)this.random.Next();
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.GetShort(i));
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).GetShort(i));
                }
            }
        }

        [Fact]
        public void TestRandomUnsignedShortAccess() => this.TestRandomUnsignedShortAccess(true);

        [Fact]
        public void TestRandomUnsignedShortLeAccess() => this.TestRandomUnsignedShortAccess(false);

        void TestRandomUnsignedShortAccess(bool testBigEndian)
        {
            for (int i = 0; i < this.buffer.Capacity - 1; i += 2)
            {
                ushort value = (ushort)(this.random.Next() & 0xFFFF);
                if (testBigEndian)
                {
                    this.buffer.SetUnsignedShort(i, value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).SetUnsignedShort(i, value);
                }
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 1; i += 2)
            {
                int value = this.random.Next() & 0xFFFF;
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.GetUnsignedShort(i));
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).GetUnsignedShort(i));
                }
            }
        }

        [Fact]
        public void TestRandomIntAccess() => this.TestRandomIntAccess(true);

        [Fact]
        public void TestRandomIntLeAccess() => this.TestRandomIntAccess(false);

        void TestRandomIntAccess(bool testBigEndian)
        {
            for (int i = 0; i < this.buffer.Capacity - 3; i += 4)
            {
                int value = this.random.Next();
                if (testBigEndian)
                {
                    this.buffer.SetInt(i, value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).SetInt(i, value);
                }
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 3; i += 4)
            {
                int value = this.random.Next();
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.GetInt(i));
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).GetInt(i));
                }
            }
        }

        [Fact]
        public void TestRandomUnsignedIntAccess() => this.TestRandomUnsignedIntAccess(true);

        [Fact]
        public void TestRandomUnsignedIntLeAccess() => this.TestRandomUnsignedIntAccess(false);

        void TestRandomUnsignedIntAccess(bool testBigEndian)
        {
            for (int i = 0; i < this.buffer.Capacity - 3; i += 4)
            {
                uint value = (uint)(this.random.Next() & 0xFFFFFFFFL);
                if (testBigEndian)
                {
                    this.buffer.SetUnsignedInt(i, value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).SetUnsignedInt(i, value);
                }
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 3; i += 4)
            {
                long value = this.random.Next() & 0xFFFFFFFFL;
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.GetUnsignedInt(i));
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).GetUnsignedInt(i));
                }
            }
        }

        [Fact]
        public void TestRandomLongAccess() => this.TestRandomLongAccess(true);

        [Fact]
        public void TestRandomLongLeAccess() => this.TestRandomLongAccess(false);

        void TestRandomLongAccess(bool testBigEndian)
        {
            for (int i = 0; i < this.buffer.Capacity - 7; i += 8)
            {
                long value = this.random.NextLong();
                if (testBigEndian)
                {
                    this.buffer.SetLong(i, value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).SetLong(i, value);
                }
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 7; i += 8)
            {
                long value = this.random.NextLong();
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.GetLong(i));
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).GetLong(i));
                }
            }
        }

        [Fact]
        public void TestRandomDoubleAccess()
        {
            for (int i = 0; i < this.buffer.Capacity - 7; i += 8)
            {
                double value = this.random.NextDouble();
                this.buffer.SetDouble(i, value);
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 7; i += 8)
            {
                double value = this.random.NextDouble();
                Assert.Equal(value, this.buffer.GetDouble(i), 2);
            }
        }

        [Fact]
        public void TestRandomFloatAccess()
        {
            for (int i = 0; i < this.buffer.Capacity - 3; i += 4)
            {
                float value = (float)this.random.NextDouble();
                this.buffer.SetFloat(i, value);
            }

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity - 3; i += 4)
            {
                float value = (float)this.random.NextDouble();
                Assert.Equal(value, this.buffer.GetFloat(i), 2);
            }
        }

        [Fact]
        public void TestSetZero()
        {
            this.buffer.Clear();
            while (this.buffer.IsWritable())
            {
                this.buffer.WriteByte((byte)0xFF);
            }

            for (int i = 0; i < this.buffer.Capacity;)
            {
                int length = Math.Min(this.buffer.Capacity - i, random.Next(32));
                this.buffer.SetZero(i, length);
                i += length;
            }

            for (int i = 0; i < this.buffer.Capacity; i++)
            {
                Assert.Equal(0, this.buffer.GetByte(i));
            }
        }

        [Fact]
        public void TestSequentialByteAccess()
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i ++)
            {
                byte value = (byte)this.random.Next();
                Assert.Equal(i, this.buffer.WriterIndex);
                Assert.True(this.buffer.IsWritable());
                this.buffer.WriteByte(value);
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsWritable());

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity; i ++)
            {
                byte value = (byte)this.random.Next();
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                Assert.Equal(value, this.buffer.ReadByte());
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void TestSequentialShortAccess() => this.TestSequentialShortAccess(true);

        [Fact]
        public void TestSequentialShortLeAccess() => this.TestSequentialShortAccess(false);

        void TestSequentialShortAccess(bool testBigEndian)
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i += 2)
            {
                short value = (short)this.random.Next();
                Assert.Equal(i, this.buffer.WriterIndex);
                Assert.True(this.buffer.IsWritable());
                if (testBigEndian)
                {
                    this.buffer.WriteShort(value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).WriteShort(value);
                }
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsWritable());

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity; i += 2)
            {
                short value = (short)this.random.Next();
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.ReadShort());
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).ReadShort());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void TestSequentialUnsignedShortAccess() => this.TestSequentialUnsignedShortAccess(true);

        [Fact]
        public void TestSequentialUnsignedShortLeAccess() => this.TestSequentialUnsignedShortAccess(true);

        void TestSequentialUnsignedShortAccess(bool testBigEndian)
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i += 2)
            {
                short value = (short)this.random.Next();
                Assert.Equal(i, this.buffer.WriterIndex);
                Assert.True(this.buffer.IsWritable());
                if (testBigEndian)
                {
                    this.buffer.WriteShort(value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).WriteShort(value);
                }
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsWritable());

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity; i += 2)
            {
                int value = this.random.Next() & 0xFFFF;
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.ReadUnsignedShort());
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).ReadUnsignedShort());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void TestSequentialMediumAccess() => this.TestSequentialMediumAccess(true);

        [Fact]
        public void TestSequentialMediumLeAccess() => this.TestSequentialMediumAccess(false);

        void TestSequentialMediumAccess(bool testBigEndian)
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity / 3 * 3; i += 3)
            {
                int value = this.random.Next();
                Assert.Equal(i, this.buffer.WriterIndex);
                Assert.True(this.buffer.IsWritable());
                if (testBigEndian)
                {
                    this.buffer.WriteMedium(value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).WriteMedium(value);
                }
            }
            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.WriterIndex);
            Assert.Equal(this.buffer.Capacity % 3, this.buffer.WritableBytes);

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity / 3 * 3; i += 3)
            {
                int value = this.random.Next() << 8 >> 8;
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.ReadMedium());
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).ReadMedium());
                }
            }

            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.WriterIndex);
            Assert.Equal(0, this.buffer.ReadableBytes);
            Assert.Equal(this.buffer.Capacity % 3, this.buffer.WritableBytes);
        }

        [Fact]
        public void TestSequentialUnsignedMediumAccess() => this.TestSequentialUnsignedMediumAccess(true);

        [Fact]
        public void TestSequentialUnsignedMediumLeAccess() => this.TestSequentialUnsignedMediumAccess(false);

        void TestSequentialUnsignedMediumAccess(bool testBigEndian)
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity / 3 * 3; i += 3)
            {
                int value = this.random.Next();
                Assert.Equal(i, this.buffer.WriterIndex);
                Assert.True(this.buffer.IsWritable());
                if (testBigEndian)
                {
                    this.buffer.WriteUnsignedMedium(value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).WriteUnsignedMedium(value);
                }
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.WriterIndex);
            Assert.Equal(this.buffer.Capacity % 3, this.buffer.WritableBytes);

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity / 3 * 3; i += 3)
            {
                int value = this.random.Next().ToUnsignedMediumInt();
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.ReadUnsignedMedium());
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).ReadUnsignedMedium());
                }
            }

            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.WriterIndex);
            Assert.Equal(0, this.buffer.ReadableBytes);
            Assert.Equal(this.buffer.Capacity % 3, this.buffer.WritableBytes);
        }

        [Fact]
        public void TestSequentialIntAccess() => this.TestSequentialIntAccess(true);

        [Fact]
        public void TestSequentialIntLeAccess() => this.TestSequentialIntAccess(false);

        void TestSequentialIntAccess(bool testBigEndian)
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i += 4)
            {
                int value = this.random.Next();
                Assert.Equal(i, this.buffer.WriterIndex);
                Assert.True(this.buffer.IsWritable());
                if (testBigEndian)
                {
                    this.buffer.WriteInt(value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).WriteInt(value);
                }
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsWritable());

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity; i += 4)
            {
                int value = this.random.Next();
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.ReadInt());
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).ReadInt());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void TestSequentialUnsignedIntAccess() => this.TestSequentialUnsignedIntAccess(true);

        [Fact]
        public void TestSequentialUnsignedIntLeAccess() => this.TestSequentialUnsignedIntAccess(false);

        void TestSequentialUnsignedIntAccess(bool testBigEndian)
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i += 4)
            {
                int value = this.random.Next();
                Assert.Equal(i, this.buffer.WriterIndex);
                Assert.True(this.buffer.IsWritable());
                if (testBigEndian)
                {
                    this.buffer.WriteInt(value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).WriteInt(value);
                }
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsWritable());

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity; i += 4)
            {
                long value = this.random.Next() & 0xFFFFFFFFL;
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.ReadUnsignedInt());
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).ReadUnsignedInt());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void TestSequentialLongAccess() => this.TestSequentialLongAccess(true);

        [Fact]
        public void TestSequentialLongLeAccess() => this.TestSequentialLongAccess(false);

        void TestSequentialLongAccess(bool testBigEndian)
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i += 8)
            {
                long value = this.random.NextLong();
                Assert.Equal(i, this.buffer.WriterIndex);
                Assert.True(this.buffer.IsWritable());
                if (testBigEndian)
                {
                    this.buffer.WriteLong(value);
                }
                else
                {
                    this.buffer.WithOrder(ByteOrder.LittleEndian).WriteLong(value);
                }
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsWritable());

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity; i += 8)
            {
                long value = this.random.NextLong();
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.ReadLong());
                }
                else
                {
                    Assert.Equal(value, this.buffer.WithOrder(ByteOrder.LittleEndian).ReadLong());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void TestByteArrayTransfer()
        {
            var value = new byte[BlockSize * 2];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(value);
                this.buffer.SetBytes(i, value, this.random.Next(BlockSize), BlockSize);
            }

            this.random = new Random(this.seed);
            var expectedValue = new byte[BlockSize * 2];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValue);
                int valueOffset = this.random.Next(BlockSize);
                this.buffer.GetBytes(i, value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue[j], value[j]);
                }
            }
        }

        [Fact]
        public void TestRandomByteArrayTransfer1()
        {
            var value = new byte[BlockSize];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(value);
                this.buffer.SetBytes(i, value);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize];
            IByteBuffer expectedValue = Unpooled.WrappedBuffer(expectedValueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                this.buffer.GetBytes(i, value);
                for (int j = 0; j < BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value[j]);
                }
            }
        }

        [Fact]
        public void TestRandomByteArrayTransfer2()
        {
            var value = new byte[BlockSize * 2];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(value);
                this.buffer.SetBytes(i, value, this.random.Next(BlockSize), BlockSize);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize * 2];
            IByteBuffer expectedValue = Unpooled.WrappedBuffer(expectedValueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                int valueOffset = this.random.Next(BlockSize);
                this.buffer.GetBytes(i, value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value[j]);
                }
            }
        }

        [Fact]
        public void TestRandomHeapBufferTransfer1()
        {
            var valueContent = new byte[BlockSize];
            IByteBuffer value = Unpooled.WrappedBuffer(valueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(valueContent);
                value.SetIndex(0, BlockSize);
                this.buffer.SetBytes(i, value);
                Assert.Equal(BlockSize, value.ReaderIndex);
                Assert.Equal(BlockSize, value.WriterIndex);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize];
            IByteBuffer expectedValue = Unpooled.WrappedBuffer(expectedValueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                value.Clear();
                this.buffer.GetBytes(i, value);
                Assert.Equal(0, value.ReaderIndex);
                Assert.Equal(BlockSize, value.WriterIndex);
                for (int j = 0; j < BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
            }
        }

        [Fact]
        public void TestRandomHeapBufferTransfer2()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = Unpooled.WrappedBuffer(valueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(valueContent);
                this.buffer.SetBytes(i, value, this.random.Next(BlockSize), BlockSize);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize * 2];
            IByteBuffer expectedValue = Unpooled.WrappedBuffer(expectedValueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                int valueOffset = this.random.Next(BlockSize);
                this.buffer.GetBytes(i, value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
            }
        }

        [Fact]
        public void TestRandomDirectBufferTransfer()
        {
            var tmp = new byte[BlockSize * 2];
            IByteBuffer value = ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(BlockSize * 2));
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(tmp);
                value.SetBytes(0, tmp, 0, value.Capacity);
                this.buffer.SetBytes(i, value, this.random.Next(BlockSize), BlockSize);
            }

            this.random = new Random(this.seed);
            IByteBuffer expectedValue = ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(BlockSize * 2));
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(tmp);
                expectedValue.SetBytes(0, tmp, 0, expectedValue.Capacity);
                int valueOffset = this.random.Next(BlockSize);
                this.buffer.GetBytes(i, value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
            }
        }

        [Fact]
        public void TestRandomByteBufferTransfer()
        {
            var value = new byte[BlockSize * 2];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(value);
                this.buffer.SetBytes(i, value, this.random.Next(BlockSize), BlockSize);
            }

            this.random = new Random(this.seed);
            var expectedValue = new byte[BlockSize * 2];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValue);
                int valueOffset = this.random.Next(BlockSize);
                this.buffer.GetBytes(i, value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue[j], value[j]);
                }
            }
        }

        [Fact]
        public void TestSequentialByteArrayTransfer1()
        {
            var value = new byte[BlockSize];
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(value);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                this.buffer.WriteBytes(value);
            }

            this.random = new Random(this.seed);
            var expectedValue = new byte[BlockSize];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValue);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                this.buffer.ReadBytes(value);
                for (int j = 0; j < BlockSize; j ++)
                {
                    Assert.Equal(expectedValue[j], value[j]);
                }
            }
        }

        [Fact]
        public void TestSequentialByteArrayTransfer2()
        {
            var value = new byte[BlockSize * 2];
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(value);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                int readerIndex = this.random.Next(BlockSize);
                this.buffer.WriteBytes(value, readerIndex, BlockSize);
            }

            this.random = new Random(this.seed);
            var expectedValue = new byte[BlockSize * 2];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValue);
                int valueOffset = this.random.Next(BlockSize);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                this.buffer.ReadBytes(value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue[j], value[j]);
                }
            }
        }

        [Fact]
        public void TestSequentialHeapBufferTransfer1()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = Unpooled.WrappedBuffer(valueContent);
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(valueContent);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                this.buffer.WriteBytes(value, this.random.Next(BlockSize), BlockSize);
                Assert.Equal(0, value.ReaderIndex);
                Assert.Equal(valueContent.Length, value.WriterIndex);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize * 2];
            IByteBuffer expectedValue = Unpooled.WrappedBuffer(expectedValueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                int valueOffset = this.random.Next(BlockSize);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                this.buffer.ReadBytes(value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
                Assert.Equal(0, value.ReaderIndex);
                Assert.Equal(valueContent.Length, value.WriterIndex);
            }
        }

        [Fact]
        public void TestSequentialHeapBufferTransfer2()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = Unpooled.WrappedBuffer(valueContent);
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(valueContent);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                int readerIndex = this.random.Next(BlockSize);
                value.SetReaderIndex(readerIndex);
                value.SetWriterIndex(readerIndex + BlockSize);
                this.buffer.WriteBytes(value);
                Assert.Equal(readerIndex + BlockSize, value.WriterIndex);
                Assert.Equal(value.WriterIndex, value.ReaderIndex);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize * 2];
            IByteBuffer expectedValue = Unpooled.WrappedBuffer(expectedValueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                int valueOffset = this.random.Next(BlockSize);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                value.SetReaderIndex(valueOffset);
                value.SetWriterIndex(valueOffset);
                this.buffer.ReadBytes(value, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
                Assert.Equal(valueOffset, value.ReaderIndex);
                Assert.Equal(valueOffset + BlockSize, value.WriterIndex);
            }
        }

        [Fact]
        public void TestSequentialDirectBufferTransfer1()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(BlockSize * 2));
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(valueContent);
                value.SetBytes(0, valueContent);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                this.buffer.WriteBytes(value, this.random.Next(BlockSize), BlockSize);
                Assert.Equal(0, value.ReaderIndex);
                Assert.Equal(0, value.WriterIndex);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize * 2];
            IByteBuffer expectedValue = ReferenceCountUtil.ReleaseLater(Unpooled.WrappedBuffer(expectedValueContent));
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                int valueOffset = this.random.Next(BlockSize);
                value.SetBytes(0, valueContent);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                this.buffer.ReadBytes(value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
                Assert.Equal(0, value.ReaderIndex);
                Assert.Equal(0, value.WriterIndex);
            }
        }

        [Fact]
        public void TestSequentialDirectBufferTransfer2()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(BlockSize * 2));
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(valueContent);
                value.SetBytes(0, valueContent);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                int readerIndex = this.random.Next(BlockSize);
                value.SetReaderIndex(0);
                value.SetWriterIndex(readerIndex + BlockSize);
                value.SetReaderIndex(readerIndex);
                this.buffer.WriteBytes(value);
                Assert.Equal(readerIndex + BlockSize, value.WriterIndex);
                Assert.Equal(value.WriterIndex, value.ReaderIndex);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize * 2];
            IByteBuffer expectedValue = ReferenceCountUtil.ReleaseLater(Unpooled.WrappedBuffer(expectedValueContent));
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                value.SetBytes(0, valueContent);
                int valueOffset = this.random.Next(BlockSize);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                value.SetReaderIndex(valueOffset);
                value.SetWriterIndex(valueOffset);
                this.buffer.ReadBytes(value, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
                Assert.Equal(valueOffset, value.ReaderIndex);
                Assert.Equal(valueOffset + BlockSize, value.WriterIndex);
            }
        }

        [Fact]
        public void TestSequentialByteBufferBackedHeapBufferTransfer1()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = Unpooled.WrappedBuffer(new byte[BlockSize * 2]);
            value.SetWriterIndex(0);
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(valueContent);
                value.SetBytes(0, valueContent);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                this.buffer.WriteBytes(value, this.random.Next(BlockSize), BlockSize);
                Assert.Equal(0, value.ReaderIndex);
                Assert.Equal(0, value.WriterIndex);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize * 2];
            IByteBuffer expectedValue = Unpooled.WrappedBuffer(expectedValueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                int valueOffset = this.random.Next(BlockSize);
                value.SetBytes(0, valueContent);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                this.buffer.ReadBytes(value, valueOffset, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
                Assert.Equal(0, value.ReaderIndex);
                Assert.Equal(0, value.WriterIndex);
            }
        }

        [Fact]
        public void TestSequentialByteBufferBackedHeapBufferTransfer2()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = Unpooled.WrappedBuffer(new byte[BlockSize * 2]);
            value.SetWriterIndex(0);
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(valueContent);
                value.SetBytes(0, valueContent);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                int readerIndex = this.random.Next(BlockSize);
                value.SetReaderIndex(0);
                value.SetWriterIndex(readerIndex + BlockSize);
                value.SetReaderIndex(readerIndex);
                this.buffer.WriteBytes(value);
                Assert.Equal(readerIndex + BlockSize, value.WriterIndex);
                Assert.Equal(value.WriterIndex, value.ReaderIndex);
            }

            this.random = new Random(this.seed);
            var expectedValueContent = new byte[BlockSize * 2];
            IByteBuffer expectedValue = Unpooled.WrappedBuffer(expectedValueContent);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValueContent);
                value.SetBytes(0, valueContent);
                int valueOffset = this.random.Next(BlockSize);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                value.SetReaderIndex(valueOffset);
                value.SetWriterIndex(valueOffset);
                this.buffer.ReadBytes(value, BlockSize);
                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue.GetByte(j), value.GetByte(j));
                }
                Assert.Equal(valueOffset, value.ReaderIndex);
                Assert.Equal(valueOffset + BlockSize, value.WriterIndex);
            }
        }

        [Fact]
        public void TestSequentialByteBufferTransfer()
        {
            this.buffer.SetWriterIndex(0);
            var value = new byte[BlockSize * 2];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(value);
                this.buffer.WriteBytes(value, this.random.Next(BlockSize), BlockSize);
            }

            this.random = new Random(this.seed);
            var expectedValue = new byte[BlockSize * 2];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValue);
                int valueOffset = this.random.Next(BlockSize);
                this.buffer.ReadBytes(value, valueOffset, BlockSize);

                for (int j = valueOffset; j < valueOffset + BlockSize; j ++)
                {
                    Assert.Equal(expectedValue[j], value[j]);
                }
            }
        }

        [Fact]
        public void TestSequentialCopiedBufferTransfer1()
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                var value = new byte[BlockSize];
                this.random.NextBytes(value);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                this.buffer.WriteBytes(value);
            }

            this.random = new Random(this.seed);
            var expectedValue = new byte[BlockSize];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValue);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                IByteBuffer actualValue = this.buffer.ReadBytes(BlockSize);
                Assert.Equal(Unpooled.WrappedBuffer(expectedValue), actualValue, EqualityComparer<IByteBuffer>.Default);

                // Make sure if it is a copied this.buffer.
                actualValue.SetByte(0, (byte)(actualValue.GetByte(0) + 1));
                Assert.False(this.buffer.GetByte(i) == actualValue.GetByte(0));
                actualValue.Release();
            }
        }

        [Fact]
        public void TestSequentialSlice1()
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                var value = new byte[BlockSize];
                this.random.NextBytes(value);
                Assert.Equal(0, this.buffer.ReaderIndex);
                Assert.Equal(i, this.buffer.WriterIndex);
                this.buffer.WriteBytes(value);
            }

            this.random = new Random(this.seed);
            var expectedValue = new byte[BlockSize];
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(expectedValue);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.Equal(Capacity, this.buffer.WriterIndex);
                IByteBuffer actualValue = this.buffer.ReadSlice(BlockSize);
                Assert.Equal(this.buffer.Order, actualValue.Order);
                Assert.Equal(Unpooled.WrappedBuffer(expectedValue), actualValue, EqualityComparer<IByteBuffer>.Default);

                // Make sure if it is a sliced this.buffer.
                actualValue.SetByte(0, (byte)(actualValue.GetByte(0) + 1));
                Assert.Equal(this.buffer.GetByte(i), actualValue.GetByte(0));
            }
        }

        [Fact]
        public void TestWriteZero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => this.buffer.WriteZero(-1));

            this.buffer.Clear();
            while (this.buffer.IsWritable())
            {
                this.buffer.WriteByte((byte)0xFF);
            }

            this.buffer.Clear();
            for (int i = 0; i < this.buffer.Capacity;)
            {
                int length = Math.Min(this.buffer.Capacity - i, random.Next(32));
                this.buffer.WriteZero(length);
                i += length;
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(buffer.Capacity, buffer.WriterIndex);

            for (int i = 0; i < this.buffer.Capacity; i++)
            {
                Assert.Equal(0, this.buffer.GetByte(i));
            }
        }

        [Fact]
        public void TestDiscardReadBytes()
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i += 4)
            {
                this.buffer.WriteInt(i);
            }
            IByteBuffer copy = Unpooled.CopiedBuffer(this.buffer);

            // Make sure there's no effect if called when readerIndex is 0.
            this.buffer.SetReaderIndex(Capacity / 4);
            this.buffer.MarkReaderIndex();
            this.buffer.SetWriterIndex(Capacity / 3);
            this.buffer.MarkWriterIndex();
            this.buffer.SetReaderIndex(0);
            this.buffer.SetWriterIndex(Capacity / 2);
            this.buffer.DiscardReadBytes();

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(Capacity / 2, this.buffer.WriterIndex);
            Assert.Equal(copy.Slice(0, Capacity / 2), this.buffer.Slice(0, Capacity / 2));
            this.buffer.ResetReaderIndex();
            Assert.Equal(Capacity / 4, this.buffer.ReaderIndex);
            this.buffer.ResetWriterIndex();
            Assert.Equal(Capacity / 3, this.buffer.WriterIndex);

            // Make sure bytes after writerIndex is not copied.
            this.buffer.SetReaderIndex(1);
            this.buffer.SetWriterIndex(Capacity / 2);
            this.buffer.DiscardReadBytes();

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(Capacity / 2 - 1, this.buffer.WriterIndex);
            Assert.Equal(copy.Slice(1, Capacity / 2 - 1), this.buffer.Slice(0, Capacity / 2 - 1));

            if (this.DiscardReadBytesDoesNotMoveWritableBytes())
            {
                // If writable bytes were copied, the test should fail to avoid unnecessary memory bandwidth consumption.
                Assert.False(copy.Slice(Capacity / 2, Capacity / 2).Equals(this.buffer.Slice(Capacity / 2 - 1, Capacity / 2)));
            }
            else
            {
                Assert.Equal(copy.Slice(Capacity / 2, Capacity / 2), this.buffer.Slice(Capacity / 2 - 1, Capacity / 2));
            }

            // Marks also should be relocated.
            this.buffer.ResetReaderIndex();
            Assert.Equal(Capacity / 4 - 1, this.buffer.ReaderIndex);
            this.buffer.ResetWriterIndex();
            Assert.Equal(Capacity / 3 - 1, this.buffer.WriterIndex);

            copy.Release();
        }

        /**
         * The similar test case with {@link #testDiscardReadBytes()} but this one
         * discards a large chunk at once.
         */

        [Fact]
        public void TestDiscardReadBytes2()
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i ++)
            {
                this.buffer.WriteByte((byte)i);
            }
            IByteBuffer copy = ReferenceCountUtil.ReleaseLater(Unpooled.CopiedBuffer(this.buffer));

            // Discard the first (CAPACITY / 2 - 1) bytes.
            this.buffer.SetIndex(Capacity / 2 - 1, Capacity - 1);
            this.buffer.DiscardReadBytes();
            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(Capacity / 2, this.buffer.WriterIndex);
            for (int i = 0; i < Capacity / 2; i ++)
            {
                Assert.Equal(copy.Slice(Capacity / 2 - 1 + i, Capacity / 2 - i), this.buffer.Slice(i, Capacity / 2 - i));
            }
        }

        [Fact]
        public void TestCopy()
        {
            for (int i = 0; i < this.buffer.Capacity; i ++)
            {
                byte value = (byte)this.random.Next();
                this.buffer.SetByte(i, value);
            }

            int readerIndex = Capacity / 3;
            int writerIndex = Capacity * 2 / 3;
            this.buffer.SetIndex(readerIndex, writerIndex);

            // Make sure all properties are copied.
            IByteBuffer copy = ReferenceCountUtil.ReleaseLater(this.buffer.Copy());
            Assert.Equal(0, copy.ReaderIndex);
            Assert.Equal(this.buffer.ReadableBytes, copy.WriterIndex);
            Assert.Equal(this.buffer.ReadableBytes, copy.Capacity);
            Assert.Equal(this.buffer.Order, copy.Order);
            for (int i = 0; i < copy.Capacity; i ++)
            {
                Assert.Equal(this.buffer.GetByte(i + readerIndex), copy.GetByte(i));
            }

            // Make sure the this.buffer content is independent from each other.
            this.buffer.SetByte(readerIndex, (byte)(this.buffer.GetByte(readerIndex) + 1));
            Assert.True(this.buffer.GetByte(readerIndex) != copy.GetByte(0));
            copy.SetByte(1, (byte)(copy.GetByte(1) + 1));
            Assert.True(this.buffer.GetByte(readerIndex + 1) != copy.GetByte(1));
        }

        [Fact]
        public void TestDuplicate()
        {
            for (int i = 0; i < this.buffer.Capacity; i ++)
            {
                byte value = (byte)this.random.Next();
                this.buffer.SetByte(i, value);
            }

            int readerIndex = Capacity / 3;
            int writerIndex = Capacity * 2 / 3;
            this.buffer.SetIndex(readerIndex, writerIndex);

            // Make sure all properties are copied.
            IByteBuffer duplicate = this.buffer.Duplicate();
            Assert.Equal(this.buffer.ReaderIndex, duplicate.ReaderIndex);
            Assert.Equal(this.buffer.WriterIndex, duplicate.WriterIndex);
            Assert.Equal(this.buffer.Capacity, duplicate.Capacity);
            Assert.Equal(this.buffer.Order, duplicate.Order);
            for (int i = 0; i < duplicate.Capacity; i ++)
            {
                Assert.Equal(this.buffer.GetByte(i), duplicate.GetByte(i));
            }

            // Make sure the this.buffer content is shared.
            this.buffer.SetByte(readerIndex, (byte)(this.buffer.GetByte(readerIndex) + 1));
            Assert.Equal(this.buffer.GetByte(readerIndex), duplicate.GetByte(readerIndex));
            duplicate.SetByte(1, (byte)(duplicate.GetByte(1) + 1));
            Assert.Equal(this.buffer.GetByte(1), duplicate.GetByte(1));
        }

        [Fact]
        public void TestSliceEndianness()
        {
            Assert.Equal(this.buffer.Order, this.buffer.Slice(0, this.buffer.Capacity).Order);
            Assert.Equal(this.buffer.Order, this.buffer.Slice(0, this.buffer.Capacity - 1).Order);
            Assert.Equal(this.buffer.Order, this.buffer.Slice(1, this.buffer.Capacity - 1).Order);
            Assert.Equal(this.buffer.Order, this.buffer.Slice(1, this.buffer.Capacity - 2).Order);
        }

        [Fact]
        public void TestSliceIndex()
        {
            Assert.Equal(0, this.buffer.Slice(0, this.buffer.Capacity).ReaderIndex);
            Assert.Equal(0, this.buffer.Slice(0, this.buffer.Capacity - 1).ReaderIndex);
            Assert.Equal(0, this.buffer.Slice(1, this.buffer.Capacity - 1).ReaderIndex);
            Assert.Equal(0, this.buffer.Slice(1, this.buffer.Capacity - 2).ReaderIndex);

            Assert.Equal(this.buffer.Capacity, this.buffer.Slice(0, this.buffer.Capacity).WriterIndex);
            Assert.Equal(this.buffer.Capacity - 1, this.buffer.Slice(0, this.buffer.Capacity - 1).WriterIndex);
            Assert.Equal(this.buffer.Capacity - 1, this.buffer.Slice(1, this.buffer.Capacity - 1).WriterIndex);
            Assert.Equal(this.buffer.Capacity - 2, this.buffer.Slice(1, this.buffer.Capacity - 2).WriterIndex);
        }

        [Fact]
        public void TestEquals()
        {
            Assert.False(this.buffer.Equals(null));
            Assert.False(this.buffer.Equals(new object()));

            var value = new byte[32];
            this.buffer.SetIndex(0, value.Length);
            this.random.NextBytes(value);
            this.buffer.SetBytes(0, value);

            Assert.Equal(this.buffer, Unpooled.WrappedBuffer(value), EqualityComparer<IByteBuffer>.Default);
            Assert.Equal(this.buffer, Unpooled.WrappedBuffer(value).WithOrder(ByteOrder.LittleEndian), EqualityComparer<IByteBuffer>.Default);

            value[0] ++;
            Assert.False(this.buffer.Equals(Unpooled.WrappedBuffer(value)));
            Assert.False(this.buffer.Equals(Unpooled.WrappedBuffer(value).WithOrder(ByteOrder.LittleEndian)));
        }

        [Fact]
        public void TestCompareTo()
        {
            Assert.Throws<NullReferenceException>(() => this.buffer.CompareTo(null));

            // Fill the this.random stuff
            var value = new byte[32];
            this.random.NextBytes(value);
            // Prevent overflow / underflow
            if (value[0] == 0)
            {
                value[0] ++;
            }
            else if (value[0] == 0xFF)
            {
                value[0] --;
            }

            this.buffer.SetIndex(0, value.Length);
            this.buffer.SetBytes(0, value);

            Assert.Equal(0, this.buffer.CompareTo(Unpooled.WrappedBuffer(value)));
            Assert.Equal(0, this.buffer.CompareTo(Unpooled.WrappedBuffer(value).WithOrder(ByteOrder.LittleEndian)));

            value[0] ++;
            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value)) < 0);
            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value).WithOrder(ByteOrder.LittleEndian)) < 0);
            value[0] -= 2;
            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value)) > 0);
            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value).WithOrder(ByteOrder.LittleEndian)) > 0);
            value[0] ++;

            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value, 0, 31)) > 0);
            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value, 0, 31).WithOrder(ByteOrder.LittleEndian)) > 0);
            Assert.True(this.buffer.Slice(0, 31).CompareTo(Unpooled.WrappedBuffer(value)) < 0);
            Assert.True(this.buffer.Slice(0, 31).CompareTo(Unpooled.WrappedBuffer(value).WithOrder(ByteOrder.LittleEndian)) < 0);
        }

        [Fact]
        public void TestToString()
        {
            this.buffer.Clear();
            this.buffer.WriteBytes(ReferenceCountUtil.ReleaseLater(Unpooled.CopiedBuffer(Encoding.GetEncoding("ISO-8859-1").GetBytes("Hello, World!"))));
            Assert.Equal("Hello, World!", this.buffer.ToString(Encoding.GetEncoding("ISO-8859-1")));
        }

        [Fact]
        public void TestIoBuffer1()
        {
            if (this.buffer.IoBufferCount != 1)
            {
                // skipping
                return;
            }

            var value = new byte[this.buffer.Capacity];
            this.random.NextBytes(value);
            this.buffer.Clear();
            this.buffer.WriteBytes(value);

            AssertRemainingEquals(new ArraySegment<byte>(value), this.buffer.GetIoBuffer());
        }

        [Fact]
        public void TestToByteBuffer2()
        {
            if (this.buffer.IoBufferCount != 1)
            {
                // skipping 
                return;
            }

            var value = new byte[this.buffer.Capacity];
            this.random.NextBytes(value);
            this.buffer.Clear();
            this.buffer.WriteBytes(value);

            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                AssertRemainingEquals(new ArraySegment<byte>(value, i, BlockSize), this.buffer.GetIoBuffer(i, BlockSize));
            }
        }

        static void AssertRemainingEquals(ArraySegment<byte> expected, ArraySegment<byte> actual)
        {
            int remaining = expected.Count;
            int remaining2 = actual.Count;

            Assert.Equal(remaining, remaining2);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestSkipBytes1()
        {
            this.buffer.SetIndex(Capacity / 4, Capacity / 2);

            this.buffer.SkipBytes(Capacity / 4);
            Assert.Equal(Capacity / 4 * 2, this.buffer.ReaderIndex);

            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SkipBytes(Capacity / 4 + 1));

            // Should remain unchanged.
            Assert.Equal(Capacity / 4 * 2, this.buffer.ReaderIndex);
        }

        [Fact]
        public void TestHashCode()
        {
            IByteBuffer elemA = ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(15));
            IByteBuffer elemB = ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(15));
            elemA.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3, 4, 5 });
            elemB.WriteBytes(new byte[] { 6, 7, 8, 9, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9 });

            var set = new HashSet<IByteBuffer>();
            set.Add(elemA);
            set.Add(elemB);

            Assert.Equal(2, set.Count);
            Assert.True(set.Contains(ReferenceCountUtil.ReleaseLater(elemA.Copy())));

            IByteBuffer elemBCopy = ReferenceCountUtil.ReleaseLater(elemB.Copy());
            Assert.True(set.Contains(elemBCopy));

            this.buffer.Clear();
            this.buffer.WriteBytes(elemA.Duplicate());

            Assert.True(set.Remove(this.buffer));
            Assert.False(set.Contains(elemA));
            Assert.Equal(1, set.Count);

            this.buffer.Clear();
            this.buffer.WriteBytes(elemB.Duplicate());
            Assert.True(set.Remove(this.buffer));
            Assert.False(set.Contains(elemB));
            Assert.Equal(0, set.Count);
        }

        // Test case for https://github.com/netty/netty/issues/325
        [Fact]
        public void TestDiscardAllReadBytes()
        {
            this.buffer.SetWriterIndex(this.buffer.Capacity);
            this.buffer.SetReaderIndex(this.buffer.WriterIndex);
            this.buffer.DiscardReadBytes();
        }

        [Fact]
        public void TestForEachByte()
        {
            this.buffer.Clear();
            for (int i = 0; i < Capacity; i ++)
            {
                this.buffer.WriteByte(i + 1);
            }

            int lastIndex = 0;
            this.buffer.SetIndex(Capacity / 4, Capacity * 3 / 4);
            int i1 = Capacity / 4;
            Assert.Equal(-1,
                this.buffer.ForEachByte(new ByteProcessor.CustomProcessor(
                    value =>
                    {
                        Assert.Equal(value, (byte)(i1 + 1));
                        Volatile.Write(ref lastIndex, i1);
                        i1++;
                        return true;
                    })));

            Assert.Equal(Capacity * 3 / 4 - 1, Volatile.Read(ref lastIndex));
        }

        [Fact]
        public void TestForEachByteAbort()
        {
            this.buffer.Clear();
            for (int i = 0; i < Capacity; i ++)
            {
                this.buffer.WriteByte(i + 1);
            }

            int stop = Capacity / 2;
            int i1 = Capacity / 3;
            Assert.Equal(stop, this.buffer.ForEachByte(Capacity / 3, Capacity / 3, new ByteProcessor.CustomProcessor(value =>
            {
                Assert.Equal((byte)(i1 + 1), value);
                if (i1 == stop)
                {
                    return false;
                }

                i1++;
                return true;
            })));
        }

        [Fact]
        public void TestForEachByteDesc()
        {
            this.buffer.Clear();
            for (int i = 0; i < Capacity; i ++)
            {
                this.buffer.WriteByte(i + 1);
            }

            int lastIndex = 0;
            int i1 = Capacity * 3 / 4 - 1;
            Assert.Equal(-1, this.buffer.ForEachByteDesc(Capacity / 4, Capacity * 2 / 4, new ByteProcessor.CustomProcessor(value =>
            {
                Assert.Equal((byte)(i1 + 1), value);
                Volatile.Write(ref lastIndex, i1);
                i1 --;
                return true;
            })));

            Assert.Equal(Capacity / 4, Volatile.Read(ref lastIndex));
        }

        [Fact]
        public void TestDuplicateBytesInArrayMultipleThreads() => this.TestBytesInArrayMultipleThreads(false);

        [Fact]
        public void TestSliceBytesInArrayMultipleThreads() => this.TestBytesInArrayMultipleThreads(true);

        void TestBytesInArrayMultipleThreads(bool slice)
        {
            //byte[] bytes = new byte[8];
            //this.random.NextBytes(bytes);

            //IByteBuffer buffer = ReferenceCountUtil.ReleaseLater(this.NewBuffer(8));
            //this.buffer.WriteBytes(bytes);
            //final AtomicReference<Throwable> cause = new AtomicReference<Throwable>();
            //final CountDownLatch latch = new CountDownLatch(60000);
            //final CyclicBarrier barrier = new CyclicBarrier(11);
            //for (int i = 0; i < 10; i++) {
            //    new Thread(new Runnable() {

            //        public void run() {
            //            while (cause.get() == null && latch.getCount() > 0) {
            //                IByteBuffer buf;
            //                if (slice) {
            //                    buf = this.buffer.Slice();
            //                } else {
            //                    buf = this.buffer.Duplicate();
            //                }

            //                byte[] array = new byte[8];
            //                buf.ReadBytes(array);

            //                assertArrayEquals(bytes, array);

            //                Arrays.fill(array, (byte) 0);
            //                buf.GetBytes(0, array);
            //                assertArrayEquals(bytes, array);

            //                latch.countDown();
            //            }
            //            try {
            //                barrier.await();
            //            } catch (Exception e) {
            //                // ignore
            //            }
            //        }
            //    }).start();
            //}
            //latch.await(10, TimeUnit.SECONDS);
            //barrier.await(5, TimeUnit.SECONDS);
            //assertNull(cause.get());
        }

        [Fact]
        public void ReadByteThrowsIndexOutOfRangeException()
        {
            IByteBuffer buffer = ReferenceCountUtil.ReleaseLater(this.NewBuffer(8));
            buffer.WriteByte(0);
            Assert.Equal((byte)0, buffer.ReadByte());
            Assert.Throws<IndexOutOfRangeException>(() => buffer.ReadByte());
        }

        // See:
        // - https://github.com/netty/netty/issues/2587
        // - https://github.com/netty/netty/issues/2580
        [Fact]
        public void TestLittleEndianWithExpand()
        {
            IByteBuffer buffer = ReferenceCountUtil.ReleaseLater(this.NewBuffer(0)).WithOrder(ByteOrder.LittleEndian);
            buffer.WriteInt(0x12345678);
            Assert.Equal("78563412", ByteBufferUtil.HexDump(buffer));
        }

        IByteBuffer ReleasedBuffer()
        {
            IByteBuffer buffer = this.NewBuffer(8);
            Assert.True(buffer.Release());
            return buffer;
        }

        [Fact]
        public void TestDiscardReadBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().DiscardReadBytes());

        [Fact]
        public void TestDiscardSomeReadBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().DiscardSomeReadBytes());

        [Fact]
        public void TestEnsureWritableAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().EnsureWritable(16));

        [Fact]
        public void TestGetBooleanAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBoolean(0));

        [Fact]
        public void TestGetByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetByte(0));

        [Fact]
        public void TestGetMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetMedium(0));

        [Fact]
        public void TestGetMediumLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).GetMedium(0));

        [Fact]
        public void TestGetUnsignedMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedMedium(0));

        [Fact]
        public void TestGetUnsignedMediumLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).GetUnsignedMedium(0));

        [Fact]
        public void TestGetShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetShort(0));

        [Fact]
        public void TestGetShortLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).GetShort(0));

        [Fact]
        public void TestGetUnsignedShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedShort(0));

        [Fact]
        public void TestGetUnsignedShortLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).GetUnsignedShort(0));

        [Fact]
        public void TestGetIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetInt(0));

        [Fact]
        public void TestGetIntLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).GetInt(0));

        [Fact]
        public void TestGetUnsignedIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedInt(0));

        [Fact]
        public void TestGetUnsignedIntLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).GetUnsignedInt(0));

        [Fact]
        public void TestGetLongAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetLong(0));

        [Fact]
        public void TestGetLongLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).GetLong(0));

        [Fact]
        public void TestGetCharAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetChar(0));

        [Fact]
        public void TestGetDoubleAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetDouble(0));

        [Fact]
        public void TestGetFloatAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetFloat(0));

        [Fact]
        public void TestGetBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(8))));

        [Fact]
        public void TestGetBytesAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, ReferenceCountUtil.ReleaseLater(Unpooled.Buffer()), 1));

        [Fact]
        public void TestGetBytesAfterRelease3() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, ReferenceCountUtil.ReleaseLater(Unpooled.Buffer()), 0, 1));

        [Fact]
        public void TestGetBytesAfterRelease4() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, new byte[8]));

        [Fact]
        public void TestGetBytesAfterRelease5() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, new byte[8], 0, 1));

        [Fact]
        public void TestSetBooleanAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBoolean(0, true));

        [Fact]
        public void TestSetByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetByte(0, 1));

        [Fact]
        public void TestSetShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetShort(0, 1));

        [Fact]
        public void TestSetMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetMedium(0, 1));

        [Fact]
        public void TestSetMediumLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).SetMedium(0, 1));

        [Fact]
        public void TestSetShortLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).SetShort(0, 1));

        [Fact]
        public void TestSetUnsignedShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetUnsignedShort(0, 1));

        [Fact]
        public void TestSetUnsignedShortLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).SetUnsignedShort(0, 1));

        [Fact]
        public void TestSetIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetInt(0, 1));

        [Fact]
        public void TestSetIntLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).SetInt(0, 1));

        [Fact]
        public void TestSetUnsignedIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetUnsignedInt(0, 1));

        [Fact]
        public void TestSetUnsignedIntLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).SetUnsignedInt(0, 1));

        [Fact]
        public void TestSetLongAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetLong(0, 1));

        [Fact]
        public void TestSetLongLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).SetLong(0, 1));

        [Fact]
        public void TestSetCharAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetChar(0, (char)1));

        [Fact]
        public void TestSetDoubleAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetDouble(0, 1));

        [Fact]
        public void TestSetFloatAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetFloat(0, 1));

        [Fact]
        public void TestSetBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, ReferenceCountUtil.ReleaseLater(Unpooled.Buffer())));

        [Fact]
        public void TestSetBytesAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, ReferenceCountUtil.ReleaseLater(Unpooled.Buffer()), 1));

        [Fact]
        public void TestSetBytesAfterRelease3() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, ReferenceCountUtil.ReleaseLater(Unpooled.Buffer()), 0, 1));

        [Fact]
        public void TestSetBytesAfterRelease4() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, new byte[8]));

        [Fact]
        public void TestSetBytesAfterRelease5() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, new byte[8], 0, 1));

        [Fact]
        public void TestSetZeroAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetZero(0, 1));

        [Fact]
        public void TestReadBooleanAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBoolean());

        [Fact]
        public void TestReadByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadByte());

        [Fact]
        public void TestReadShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadShort());

        [Fact]
        public void TestReadShortLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).ReadShort());

        [Fact]
        public void TestReadMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadMedium());

        [Fact]
        public void TestReadMediumLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).ReadMedium());

        [Fact]
        public void TestReadUnsignedMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedMedium());

        [Fact]
        public void TestReadUnsignedMediumLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).ReadUnsignedMedium());

        [Fact]
        public void TestReadUnsignedShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedShort());

        [Fact]
        public void TestReadUnsignedShortLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).ReadUnsignedShort());

        [Fact]
        public void TestReadIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadInt());

        [Fact]
        public void TestReadIntLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).ReadInt());

        [Fact]
        public void TestReadUnsignedIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedInt());

        [Fact]
        public void TestReadUnsignedIntLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).ReadUnsignedInt());

        [Fact]
        public void TestReadLongAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadLong());

        [Fact]
        public void TestReadLongLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).ReadLong());

        [Fact]
        public void TestReadCharAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadChar());

        [Fact]
        public void TestReadDoubleAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadDouble());

        [Fact]
        public void TestReadFloatAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadFloat());

        [Fact]
        public void TestReadBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(1));

        [Fact]
        public void TestReadBytesAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(8))));

        [Fact]
        public void TestReadBytesAfterRelease3() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(8), 1)));

        [Fact]
        public void TestReadBytesAfterRelease4() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(8)), 0, 1));

        [Fact]
        public void TestReadBytesAfterRelease5() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(new byte[8]));

        [Fact]
        public void TestReadBytesAfterRelease6() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(new byte[8], 0, 1));

        [Fact]
        public void TestWriteBooleanAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBoolean(true));

        [Fact]
        public void TestWriteByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteByte(1));

        [Fact]
        public void TestWriteMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteMedium(1));

        [Fact]
        public void TestWriteMediumLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).WriteMedium(1));

        [Fact]
        public void TestWriteShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteShort(1));

        [Fact]
        public void TestWriteShortLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).WriteShort(1));

        [Fact]
        public void TestWriteUnsignedShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteUnsignedShort(1));

        [Fact]
        public void TestWriteUnsignedShortLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).WriteUnsignedShort(1));

        [Fact]
        public void TestWriteIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteInt(1));

        [Fact]
        public void TestWriteIntLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).WriteInt(1));

        [Fact]
        public void TestWriteUnsignedIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteUnsignedInt(1));

        [Fact]
        public void TestWriteUnsignedIntLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).WriteUnsignedInt(1));

        [Fact]
        public void TestWriteLongAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteLong(1));

        [Fact]
        public void TestWriteLongLeAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WithOrder(ByteOrder.LittleEndian).WriteLong(1));

        [Fact]
        public void TestWriteCharAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteChar((char)1));

        [Fact]
        public void TestWriteDoubleAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteDouble(1));

        [Fact]
        public void TestWriteFloatAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteFloat(1));

        [Fact]
        public void TestWriteBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(8))));

        [Fact]
        public void TestWriteBytesAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(ReferenceCountUtil.ReleaseLater(Unpooled.CopiedBuffer(new byte[8])), 1));

        [Fact]
        public void TestWriteBytesAfterRelease3() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(8)), 0, 1));

        [Fact]
        public void TestWriteBytesAfterRelease4() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(new byte[8]));

        [Fact]
        public void TestWriteBytesAfterRelease5() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(new byte[8], 0, 1));

        [Fact]
        public void TestWriteZeroAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteZero(1));

        [Fact]
        public void TestForEachByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ForEachByte(new TestByteProcessor()));

        [Fact]
        public void TestForEachByteAfterRelease1() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ForEachByte(0, 1, new TestByteProcessor()));

        [Fact]
        public void TestForEachByteDescAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ForEachByteDesc(new TestByteProcessor()));

        [Fact]
        public void TestForEachByteDescAfterRelease1() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ForEachByteDesc(0, 1, new TestByteProcessor()));

        [Fact]
        public void TestCopyAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().Copy());

        [Fact]
        public void TestCopyAfterRelease1() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().Copy());

        [Fact]
        public void TestIoBufferAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIoBuffer());

        [Fact]
        public void TestIoBufferAfterRelease1() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIoBuffer(0, 1));

        [Fact]
        public void TestIoBuffersAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIoBuffers());

        [Fact]
        public void TestIoBuffersAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIoBuffers(0, 1));

        [Fact]
        public void TestArrayAfterRelease()
        {
            IByteBuffer buf = this.ReleasedBuffer();
            if (buf.HasArray)
            {
                Assert.Throws<IllegalReferenceCountException>(() =>
                {
                    byte[] a = buf.Array;
                });
            }
        }

        [Fact]
        public void TestSliceRelease()
        {
            IByteBuffer buf = this.NewBuffer(8);
            Assert.Equal(1, buf.ReferenceCount);
            Assert.True(buf.Slice().Release());
            Assert.Equal(0, buf.ReferenceCount);
        }

        [Fact]
        public void TestDuplicateRelease()
        {
            IByteBuffer buf = this.NewBuffer(8);
            Assert.Equal(1, buf.ReferenceCount);
            Assert.True(buf.Duplicate().Release());
            Assert.Equal(0, buf.ReferenceCount);
        }

        [Fact]
        public void TestReadBytes()
        {
            IByteBuffer buffer = this.NewBuffer(8);
            var bytes = new byte[8];
            buffer.WriteBytes(bytes);

            IByteBuffer buffer2 = buffer.ReadBytes(4);
            Assert.Same(buffer.Allocator, buffer2.Allocator);
            Assert.Equal(4, buffer.ReaderIndex);
            Assert.True(buffer.Release());
            Assert.Equal(0, buffer.ReferenceCount);
            Assert.True(buffer2.Release());
            Assert.Equal(0, buffer2.ReferenceCount);
        }

        // Test-case trying to reproduce:
        // https://github.com/netty/netty/issues/2843
        [Fact]
        public void TestRefCnt() => this.TestRefCnt0(false);

        // Test-case trying to reproduce:
        // https://github.com/netty/netty/issues/2843
        [Fact]
        public void TestRefCnt2() => this.TestRefCnt0(true);

        void TestRefCnt0(bool parameter)
        {
            for (int i = 0; i < 10; i++)
            {
                var latch = new ManualResetEventSlim();
                var innerLatch = new ManualResetEventSlim();

                IByteBuffer buffer = this.NewBuffer(4);
                Assert.Equal(1, buffer.ReferenceCount);
                int cnt = int.MaxValue;
                var t1 = new Thread(s =>
                {
                    bool released;
                    if (parameter)
                    {
                        released = buffer.Release(buffer.ReferenceCount);
                    }
                    else
                    {
                        released = buffer.Release();
                    }
                    Assert.True(released);
                    var t2 = new Thread(s2 =>
                    {
                        Volatile.Write(ref cnt, buffer.ReferenceCount);
                        latch.Set();
                    });
                    t2.Start();
                    // Keep Thread alive a bit so the ThreadLocal caches are not freed
                    innerLatch.Wait();
                });
                t1.Start();

                latch.Wait();
                Assert.Equal(0, Volatile.Read(ref cnt));
                innerLatch.Set();
            }
        }

        [Fact]
        public void TestEmptyIoBuffers()
        {
            IByteBuffer buffer = ReferenceCountUtil.ReleaseLater(this.NewBuffer(8));
            buffer.Clear();
            Assert.False(buffer.IsReadable());
            ArraySegment<byte>[] nioBuffers = buffer.GetIoBuffers();
            Assert.Equal(1, nioBuffers.Length);
            Assert.Equal(0, nioBuffers[0].Count);
        }

        sealed class TestByteProcessor : ByteProcessor
        {
            public override bool Process(byte value) => true;
        }
    }
}