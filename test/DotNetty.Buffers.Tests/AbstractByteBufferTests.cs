// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using DotNetty.Common.Utilities;
    using Xunit;

    public abstract class AbstractByteBufferTests : IDisposable
    {
        const int Capacity = 4096; // Must be even
        const int BlockSize = 128;

        readonly Queue<IByteBuffer> buffers = new Queue<IByteBuffer>();

        readonly int seed;
        Random random;
        IByteBuffer buffer;

        // This is used for detection byte buffer types assume int.MaxValue
        // to be the MaxCapacity such as CompositeByteBuffer so that the 
        // MaxCapacity related tests do not run
        protected bool AssumedMaxCapacity = false;

        protected IByteBuffer NewBuffer(int capacity) => this.NewBuffer(capacity, int.MaxValue);

        protected abstract IByteBuffer NewBuffer(int capacity, int maxCapacity);

        protected virtual bool DiscardReadBytesDoesNotMoveWritableBytes() => true;

        protected AbstractByteBufferTests()
        {
            this.buffer = this.NewBuffer(Capacity);
            this.seed = Environment.TickCount;
            this.random = new Random(this.seed);
        }

        public virtual void Dispose()
        {
            if (this.buffer != null)
            {
                Assert.True(this.buffer.Release());
                Assert.Equal(0, this.buffer.ReferenceCount);

                try
                {
                    this.buffer.Release();
                }
                catch
                {
                    // Ignore.
                }

                this.buffer = null;
            }

            for (;;)
            {
                IByteBuffer buf = null;
                if (this.buffers.Count > 0)
                {
                    buf = this.buffers.Dequeue();
                }

                if (buf == null)
                {
                    break;
                }

                try
                {
                    buf.Release();
                }
                catch
                {
                    // Ignore.
                }
            }
        }

        [Fact]
        public void ComparableInterfaceNotViolated()
        {
            this.buffer.SetWriterIndex(this.buffer.ReaderIndex);
            Assert.True(this.buffer.WritableBytes >= 4);

            this.buffer.WriteLong(0);
            IByteBuffer buffer2 = this.NewBuffer(Capacity);

            buffer2.SetWriterIndex(buffer2.ReaderIndex);
            // Write an unsigned integer that will cause buffer.getUnsignedInt() - buffer2.getUnsignedInt() to underflow the
            // int type and wrap around on the negative side.
            buffer2.WriteLong(0xF0000000L);
            Assert.True(this.buffer.CompareTo(buffer2) < 0);
            Assert.True(buffer2.CompareTo(this.buffer) > 0);
            buffer2.Release();
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
        public void GetShortLEBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetShortLE(-1));

        [Fact]
        public void GetShortLEBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetShortLE(this.buffer.Capacity - 1));

        [Fact]
        public void GetMediumBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetMedium(-1));

        [Fact]
        public void GetMediumBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetMedium(this.buffer.Capacity - 2));

        [Fact]
        public void GetMediumLEBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetMediumLE(-1));

        [Fact]
        public void GetMediumLEBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetMediumLE(this.buffer.Capacity - 2));

        [Fact]
        public void GetIntBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetInt(-1));

        [Fact]
        public void GetIntBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetInt(this.buffer.Capacity - 3));

        [Fact]
        public void GetIntLEBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetIntLE(-1));

        [Fact]
        public void GetIntLEBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetIntLE(this.buffer.Capacity - 3));

        [Fact]
        public void GetLongBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetLong(-1));

        [Fact]
        public void GetLongBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetLong(this.buffer.Capacity - 7));

        [Fact]
        public void GetLongLEBoundaryCheck1() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetLongLE(-1));

        [Fact]
        public void GetLongLEBoundaryCheck2() => Assert.Throws<IndexOutOfRangeException>(() => this.buffer.GetLongLE(this.buffer.Capacity - 7));

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
        public void RandomByteAccess()
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
        public void RandomShortAccess() => this.RandomShortAccess0(true);

        [Fact]
        public void RandomShortLEAccess() => this.RandomShortAccess0(false);

        void RandomShortAccess0(bool testBigEndian)
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
                    this.buffer.SetShortLE(i, value);
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
                    Assert.Equal(value, this.buffer.GetShortLE(i));
                }
            }
        }

        [Fact]
        public void RandomUnsignedShortAccess() => this.RandomUnsignedShortAccess0(true);

        [Fact]
        public void RandomUnsignedShortLEAccess() => this.RandomUnsignedShortAccess0(false);

        void RandomUnsignedShortAccess0(bool testBigEndian)
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
                    this.buffer.SetUnsignedShortLE(i, value);
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
                    Assert.Equal(value, this.buffer.GetUnsignedShortLE(i));
                }
            }
        }

        [Fact]
        public void RandomMediumAccess() => this.RandomMediumAccess0(true);

        [Fact]
        public void RandomMediumLEAccess() => this.RandomMediumAccess0(false);

        void RandomMediumAccess0(bool testBigEndian)
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
                    this.buffer.SetMediumLE(i, value);
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
                    Assert.Equal(value, this.buffer.GetMediumLE(i));
                }
            }
        }

        [Fact]
        public void RandomUnsignedMediumAccess() => this.RandomUnsignedMediumAccess0(true);

        [Fact]
        public void RandomUnsignedMediumLEAccess() => this.RandomUnsignedMediumAccess0(false);

        void RandomUnsignedMediumAccess0(bool testBigEndian)
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
                    this.buffer.SetMediumLE(i, value);
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
                    Assert.Equal(value, this.buffer.GetUnsignedMediumLE(i));
                }
            }
        }

        [Fact]
        public void RandomIntAccess() => this.RandomIntAccess0(true);

        [Fact]
        public void RandomIntLEAccess() => this.RandomIntAccess0(false);

        void RandomIntAccess0(bool testBigEndian)
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
                    this.buffer.SetIntLE(i, value);
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
                    Assert.Equal(value, this.buffer.GetIntLE(i));
                }
            }
        }

        [Fact]
        public void RandomUnsignedIntAccess() => this.RandomUnsignedIntAccess0(true);

        [Fact]
        public void RandomUnsignedIntLEAccess() => this.RandomUnsignedIntAccess0(false);

        void RandomUnsignedIntAccess0(bool testBigEndian)
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
                    this.buffer.SetUnsignedIntLE(i, value);
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
                    Assert.Equal(value, this.buffer.GetUnsignedIntLE(i));
                }
            }
        }

        [Fact]
        public void RandomLongAccess() => this.RandomLongAccess0(true);

        [Fact]
        public void RandomLongLEAccess() => this.RandomLongAccess0(false);

        void RandomLongAccess0(bool testBigEndian)
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
                    this.buffer.SetLongLE(i, value);
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
                    Assert.Equal(value, this.buffer.GetLongLE(i));
                }
            }
        }

        [Fact]
        public void RandomFloatAccess()
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
        public void RandomDoubleAccess()
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
        public void SetZero()
        {
            this.buffer.Clear();
            while (this.buffer.IsWritable())
            {
                this.buffer.WriteByte(0xFF);
            }

            for (int i = 0; i < this.buffer.Capacity;)
            {
                int length = Math.Min(this.buffer.Capacity - i, this.random.Next(32));
                this.buffer.SetZero(i, length);
                i += length;
            }

            for (int i = 0; i < this.buffer.Capacity; i++)
            {
                Assert.Equal(0, this.buffer.GetByte(i));
            }
        }

        [Fact]
        public void SequentialByteAccess()
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
        public void SequentialShortAccess() => this.SequentialShortAccess0(true);

        [Fact]
        public void SequentialShortLEAccess() => this.SequentialShortAccess0(false);

        void SequentialShortAccess0(bool testBigEndian)
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
                    this.buffer.WriteShortLE(value);
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
                    Assert.Equal(value, this.buffer.ReadShortLE());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void SequentialUnsignedShortAccess() => this.SequentialUnsignedShortAccess0(true);

        [Fact]
        public void SequentialUnsignedShortLEAccess() => this.SequentialUnsignedShortAccess0(true);

        void SequentialUnsignedShortAccess0(bool testBigEndian)
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
                    this.buffer.WriteShortLE(value);
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
                    Assert.Equal(value, this.buffer.ReadUnsignedShortLE());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void SequentialMediumAccess() => this.SequentialMediumAccess0(true);

        [Fact]
        public void SequentialMediumLEAccess() => this.SequentialMediumAccess0(false);

        void SequentialMediumAccess0(bool testBigEndian)
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
                    this.buffer.WriteMediumLE(value);
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
                    Assert.Equal(value, this.buffer.ReadMediumLE());
                }
            }

            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.WriterIndex);
            Assert.Equal(0, this.buffer.ReadableBytes);
            Assert.Equal(this.buffer.Capacity % 3, this.buffer.WritableBytes);
        }

        [Fact]
        public void SequentialUnsignedMediumAccess() => this.SequentialUnsignedMediumAccess0(true);

        [Fact]
        public void SequentialUnsignedMediumLEAccess() => this.SequentialUnsignedMediumAccess0(false);

        void SequentialUnsignedMediumAccess0(bool testBigEndian)
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
                    this.buffer.WriteMediumLE(value);
                }
            }
            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.WriterIndex);
            Assert.Equal(this.buffer.Capacity % 3, this.buffer.WritableBytes);

            this.random = new Random(this.seed);
            for (int i = 0; i < this.buffer.Capacity / 3 * 3; i += 3)
            {
                int value = (this.random.Next() << 8).RightUShift(8);
                Assert.Equal(i, this.buffer.ReaderIndex);
                Assert.True(this.buffer.IsReadable());
                if (testBigEndian)
                {
                    Assert.Equal(value, this.buffer.ReadUnsignedMedium());
                }
                else
                {
                    Assert.Equal(value, this.buffer.ReadUnsignedMediumLE());
                }
            }

            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity / 3 * 3, this.buffer.WriterIndex);
            Assert.Equal(0, this.buffer.ReadableBytes);
            Assert.Equal(this.buffer.Capacity % 3, this.buffer.WritableBytes);
        }

        [Fact]
        public void SequentialIntAccess() => this.SequentialIntAccess0(true);

        [Fact]
        public void SequentialIntLEAccess() => this.SequentialIntAccess0(false);

        void SequentialIntAccess0(bool testBigEndian)
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
                    this.buffer.WriteIntLE(value);
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
                    Assert.Equal(value, this.buffer.ReadIntLE());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void SequentialUnsignedIntAccess() => this.SequentialUnsignedIntAccess0(true);

        [Fact]
        public void SequentialUnsignedIntLEAccess() => this.SequentialUnsignedIntAccess0(false);

        void SequentialUnsignedIntAccess0(bool testBigEndian)
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
                    this.buffer.WriteIntLE(value);
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
                    Assert.Equal(value, this.buffer.ReadUnsignedIntLE());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void SequentialLongAccess() => this.SequentialLongAccess0(true);

        [Fact]
        public void SequentialLongLEAccess() => this.SequentialLongAccess0(false);

        void SequentialLongAccess0(bool testBigEndian)
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
                    this.buffer.WriteLongLE(value);
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
                    Assert.Equal(value, this.buffer.ReadLongLE());
                }
            }

            Assert.Equal(this.buffer.Capacity, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);
            Assert.False(this.buffer.IsReadable());
            Assert.False(this.buffer.IsWritable());
        }

        [Fact]
        public void ByteArrayTransfer()
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
        public void RandomByteArrayTransfer1()
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
        public void RandomByteArrayTransfer2()
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
        public void RandomHeapBufferTransfer1()
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
        public void RandomHeapBufferTransfer2()
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
        public void RandomDirectBufferTransfer()
        {
            var tmp = new byte[BlockSize * 2];
            IByteBuffer value = this.ReleaseLater(Unpooled.Buffer(BlockSize * 2));
            this.buffers.Enqueue(value);
            for (int i = 0; i < this.buffer.Capacity - BlockSize + 1; i += BlockSize)
            {
                this.random.NextBytes(tmp);
                value.SetBytes(0, tmp, 0, value.Capacity);
                this.buffer.SetBytes(i, value, this.random.Next(BlockSize), BlockSize);
            }

            this.random = new Random(this.seed);
            IByteBuffer expectedValue = this.ReleaseLater(Unpooled.Buffer(BlockSize * 2));
            this.buffers.Enqueue(expectedValue);

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
        public void RandomByteBufferTransfer()
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
        public void SequentialByteArrayTransfer1()
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
        public void SequentialByteArrayTransfer2()
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
        public void SequentialHeapBufferTransfer1()
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
        public void SequentialHeapBufferTransfer2()
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
        public void SequentialDirectBufferTransfer1()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = this.ReleaseLater(Unpooled.Buffer(BlockSize * 2));
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
            IByteBuffer expectedValue = this.ReleaseLater(Unpooled.WrappedBuffer(expectedValueContent));
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
        public void SequentialDirectBufferTransfer2()
        {
            var valueContent = new byte[BlockSize * 2];
            IByteBuffer value = this.ReleaseLater(Unpooled.Buffer(BlockSize * 2));
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
            IByteBuffer expectedValue = this.ReleaseLater(Unpooled.WrappedBuffer(expectedValueContent));
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
        public void SequentialByteBufferBackedHeapBufferTransfer1()
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
        public void SequentialByteBufferBackedHeapBufferTransfer2()
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
        public void SequentialByteBufferTransfer()
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
        public void SequentialCopiedBufferTransfer1()
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
        public void SequentialSlice1()
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
                Assert.Equal(Unpooled.WrappedBuffer(expectedValue), actualValue, EqualityComparer<IByteBuffer>.Default);

                // Make sure if it is a sliced this.buffer.
                actualValue.SetByte(0, (byte)(actualValue.GetByte(0) + 1));
                Assert.Equal(this.buffer.GetByte(i), actualValue.GetByte(0));
            }
        }

        [Fact]
        public void WriteZero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => this.buffer.WriteZero(-1));

            this.buffer.Clear();
            while (this.buffer.IsWritable())
            {
                this.buffer.WriteByte(0xFF);
            }

            this.buffer.Clear();
            for (int i = 0; i < this.buffer.Capacity;)
            {
                int length = Math.Min(this.buffer.Capacity - i, this.random.Next(32));
                this.buffer.WriteZero(length);
                i += length;
            }

            Assert.Equal(0, this.buffer.ReaderIndex);
            Assert.Equal(this.buffer.Capacity, this.buffer.WriterIndex);

            for (int i = 0; i < this.buffer.Capacity; i++)
            {
                Assert.Equal(0, this.buffer.GetByte(i));
            }
        }

        [Fact]
        public void DiscardReadBytes()
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
        public void DiscardReadBytes2()
        {
            this.buffer.SetWriterIndex(0);
            for (int i = 0; i < this.buffer.Capacity; i ++)
            {
                this.buffer.WriteByte((byte)i);
            }
            IByteBuffer copy = this.ReleaseLater(Unpooled.CopiedBuffer(this.buffer));

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
        public void Copy()
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
            IByteBuffer copy = this.ReleaseLater(this.buffer.Copy());
            Assert.Equal(0, copy.ReaderIndex);
            Assert.Equal(this.buffer.ReadableBytes, copy.WriterIndex);
            Assert.Equal(this.buffer.ReadableBytes, copy.Capacity);
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
        public void Duplicate()
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
            Assert.Equal(this.buffer.ReadableBytes, duplicate.ReadableBytes);
            Assert.Equal(0, this.buffer.CompareTo(duplicate));

            // Make sure the this.buffer content is shared.
            this.buffer.SetByte(readerIndex, (byte)(this.buffer.GetByte(readerIndex) + 1));
            Assert.Equal(this.buffer.GetByte(readerIndex), duplicate.GetByte(duplicate.ReaderIndex));
            duplicate.SetByte(duplicate.ReaderIndex, (byte)(duplicate.GetByte(duplicate.ReaderIndex) + 1));
            Assert.Equal(this.buffer.GetByte(readerIndex), duplicate.GetByte(duplicate.ReaderIndex));
        }

        [Fact]
        public void SliceIndex()
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
        public void RetainedSliceIndex()
        {
            IByteBuffer retainedSlice = this.buffer.RetainedSlice(0, this.buffer.Capacity);
            Assert.Equal(0, retainedSlice.ReaderIndex);
            retainedSlice.Release();

            retainedSlice = this.buffer.RetainedSlice(0, this.buffer.Capacity - 1);
            Assert.Equal(0, retainedSlice.ReaderIndex);
            retainedSlice.Release();

            retainedSlice = this.buffer.RetainedSlice(1, this.buffer.Capacity - 1);
            Assert.Equal(0, retainedSlice.ReaderIndex);
            retainedSlice.Release();

            retainedSlice = this.buffer.RetainedSlice(1, this.buffer.Capacity - 2);
            Assert.Equal(0, retainedSlice.ReaderIndex);
            retainedSlice.Release();

            retainedSlice = this.buffer.RetainedSlice(0, this.buffer.Capacity);
            Assert.Equal(this.buffer.Capacity, retainedSlice.WriterIndex);
            retainedSlice.Release();

            retainedSlice = this.buffer.RetainedSlice(0, this.buffer.Capacity - 1);
            Assert.Equal(this.buffer.Capacity - 1, retainedSlice.WriterIndex);
            retainedSlice.Release();

            retainedSlice = this.buffer.RetainedSlice(1, this.buffer.Capacity - 1);
            Assert.Equal(this.buffer.Capacity - 1, retainedSlice.WriterIndex);
            retainedSlice.Release();

            retainedSlice = this.buffer.RetainedSlice(1, this.buffer.Capacity - 2);
            Assert.Equal(this.buffer.Capacity - 2, retainedSlice.WriterIndex);
            retainedSlice.Release();
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

            value[0] ++;
            Assert.False(this.buffer.Equals(Unpooled.WrappedBuffer(value)));
        }

        [Fact]
        public void CompareTo()
        {
            Assert.Throws<NullReferenceException>(() => this.buffer.CompareTo(null));

            // Fill the this.random stuff
            var value = new byte[32];
            this.random.NextBytes(value);
            // Prevent overflow / underflow
            if (value[0] == 0)
            {
                value[0]++;
            }
            else if (value[0] == 0xFF)
            {
                value[0]--;
            }

            this.buffer.SetIndex(0, value.Length);
            this.buffer.SetBytes(0, value);

            Assert.Equal(0, this.buffer.CompareTo(Unpooled.WrappedBuffer(value)));

            value[0]++;
            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value)) < 0);
            value[0] -= 2;
            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value)) > 0);
            value[0]++;

            Assert.True(this.buffer.CompareTo(Unpooled.WrappedBuffer(value, 0, 31)) > 0);
            Assert.True(this.buffer.Slice(0, 31).CompareTo(Unpooled.WrappedBuffer(value)) < 0);
        }

        [Fact]
        public void CompareTo2()
        {
            byte[] bytes = { 1, 2, 3, 4 };
            byte[] bytesReversed = { 4, 3, 2, 1 };

            IByteBuffer buf1 = this.NewBuffer(4).Clear().WriteBytes(bytes);
            IByteBuffer buf2 = this.NewBuffer(4).Clear().WriteBytes(bytesReversed);
            IByteBuffer buf3 = this.NewBuffer(4).Clear().WriteBytes(bytes);
            IByteBuffer buf4 = this.NewBuffer(4).Clear().WriteBytes(bytesReversed);
            try
            {
                Assert.Equal(buf1.CompareTo(buf2), buf3.CompareTo(buf4));
                Assert.Equal(buf2.CompareTo(buf1), buf4.CompareTo(buf3));
                Assert.Equal(buf1.CompareTo(buf3), buf2.CompareTo(buf4));
                Assert.Equal(buf3.CompareTo(buf1), buf4.CompareTo(buf2));
            }
            finally
            {
                buf1.Release();
                buf2.Release();
                buf3.Release();
                buf4.Release();
            }
        }

        [Fact]
        public void String()
        {
            IByteBuffer copied = Unpooled.CopiedBuffer(Encoding.GetEncoding("ISO-8859-1").GetBytes("Hello, World!"));
            this.buffer.Clear();
            this.buffer.WriteBytes(copied);
            Assert.Equal("Hello, World!", this.buffer.ToString(Encoding.GetEncoding("ISO-8859-1")));
            copied.Release();
        }

        [Fact]
        public void IndexOf()
        {
            this.buffer.Clear();
            this.buffer.WriteByte(1);
            this.buffer.WriteByte(2);
            this.buffer.WriteByte(3);
            this.buffer.WriteByte(2);
            this.buffer.WriteByte(1);

            Assert.Equal(-1, this.buffer.IndexOf(1, 4, 1));
            Assert.Equal(-1, this.buffer.IndexOf(4, 1, 1));
            Assert.Equal(1, this.buffer.IndexOf(1, 4, 2));
            Assert.Equal(3, this.buffer.IndexOf(4, 1, 2));
        }

        [Fact]
        public void IoBuffer1()
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
        public void ToByteBuffer2()
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
        public void SkipBytes1()
        {
            this.buffer.SetIndex(Capacity / 4, Capacity / 2);

            this.buffer.SkipBytes(Capacity / 4);
            Assert.Equal(Capacity / 4 * 2, this.buffer.ReaderIndex);

            Assert.Throws<IndexOutOfRangeException>(() => this.buffer.SkipBytes(Capacity / 4 + 1));

            // Should remain unchanged.
            Assert.Equal(Capacity / 4 * 2, this.buffer.ReaderIndex);
        }

        [Fact]
        public void HashCode()
        {
            IByteBuffer elemA = this.ReleaseLater(Unpooled.Buffer(15));
            IByteBuffer elemB = this.ReleaseLater(Unpooled.Buffer(15));
            elemA.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3, 4, 5 });
            elemB.WriteBytes(new byte[] { 6, 7, 8, 9, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9 });

            var set = new HashSet<IByteBuffer>();
            set.Add(elemA);
            set.Add(elemB);

            Assert.Equal(2, set.Count);
            Assert.Contains(this.ReleaseLater(elemA.Copy()), set);

            IByteBuffer elemBCopy = this.ReleaseLater(elemB.Copy());
            Assert.Contains(elemBCopy, set);

            this.buffer.Clear();
            this.buffer.WriteBytes(elemA.Duplicate());

            Assert.True(set.Remove(this.buffer));
            Assert.DoesNotContain(elemA, set);
            Assert.Single(set);

            this.buffer.Clear();
            this.buffer.WriteBytes(elemB.Duplicate());
            Assert.True(set.Remove(this.buffer));
            Assert.DoesNotContain(elemB, set);
            Assert.Empty(set);
        }

        // Test case for https://github.com/netty/netty/issues/325
        [Fact]
        public void DiscardAllReadBytes()
        {
            this.buffer.SetWriterIndex(this.buffer.Capacity);
            this.buffer.SetReaderIndex(this.buffer.WriterIndex);
            this.buffer.DiscardReadBytes();
        }

        [Fact]
        public void ForEachByte()
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
                this.buffer.ForEachByte(new ByteProcessor(
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
        public void ForEachByteAbort()
        {
            this.buffer.Clear();
            for (int i = 0; i < Capacity; i ++)
            {
                this.buffer.WriteByte(i + 1);
            }

            int stop = Capacity / 2;
            int i1 = Capacity / 3;
            Assert.Equal(stop, this.buffer.ForEachByte(Capacity / 3, Capacity / 3, new ByteProcessor(value =>
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
        public void ForEachByteDesc()
        {
            this.buffer.Clear();
            for (int i = 0; i < Capacity; i ++)
            {
                this.buffer.WriteByte(i + 1);
            }

            int lastIndex = 0;
            int i1 = Capacity * 3 / 4 - 1;
            Assert.Equal(-1, this.buffer.ForEachByteDesc(Capacity / 4, Capacity * 2 / 4, new ByteProcessor(value =>
            {
                Assert.Equal((byte)(i1 + 1), value);
                Volatile.Write(ref lastIndex, i1);
                i1 --;
                return true;
            })));

            Assert.Equal(Capacity / 4, Volatile.Read(ref lastIndex));
        }

        [Fact]
        public void DuplicateBytesInArrayMultipleThreads() => this.BytesInArrayMultipleThreads(false);

        [Fact]
        public void SliceBytesInArrayMultipleThreads() => this.BytesInArrayMultipleThreads(true);

        void BytesInArrayMultipleThreads(bool slice)
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
            IByteBuffer buf = this.NewBuffer(8);
            if (!buf.IsWritable())
            {
                Assert.Throws<IndexOutOfRangeException>(() => buf.WriteByte(0));
            }
            else
            {
                buf.WriteByte(0);
                Assert.Equal((byte)0, buf.ReadByte());
                Assert.Throws<IndexOutOfRangeException>(() => buf.ReadByte());
            }
            buf.Release();
        }

        IByteBuffer ReleasedBuffer()
        {
            IByteBuffer buf = this.NewBuffer(8);
            Assert.True(buf.Release());
            return buf;
        }

        [Fact]
        public void DiscardReadBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().DiscardReadBytes());

        [Fact]
        public void DiscardSomeReadBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().DiscardSomeReadBytes());

        [Fact]
        public void EnsureWritableAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().EnsureWritable(16));

        [Fact]
        public void GetBooleanAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBoolean(0));

        [Fact]
        public void GetByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetByte(0));

        [Fact]
        public void GetShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetShort(0));

        [Fact]
        public void GetShortLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetShortLE(0));

        [Fact]
        public void GetUnsignedShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedShort(0));

        [Fact]
        public void GetUnsignedShortLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedShortLE(0));

        [Fact]
        public void GetMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetMedium(0));

        [Fact]
        public void GetMediumLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetMediumLE(0));

        [Fact]
        public void GetUnsignedMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedMedium(0));

        [Fact]
        public void GetUnsignedMediumLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedMediumLE(0));

        [Fact]
        public void GetIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetInt(0));

        [Fact]
        public void GetIntLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIntLE(0));

        [Fact]
        public void GetUnsignedIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedInt(0));

        [Fact]
        public void GetUnsignedIntLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetUnsignedIntLE(0));

        [Fact]
        public void GetLongAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetLong(0));

        [Fact]
        public void GetLongLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetLongLE(0));

        [Fact]
        public void GetCharAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetChar(0));

        [Fact]
        public void GetFloatAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetFloat(0));

        [Fact]
        public void GetDoubleAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetDouble(0));

        [Fact]
        public void GetBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, this.ReleaseLater(Unpooled.Buffer(8))));

        [Fact]
        public void GetBytesAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, this.ReleaseLater(Unpooled.Buffer()), 1));

        [Fact]
        public void GetBytesAfterRelease3() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, this.ReleaseLater(Unpooled.Buffer()), 0, 1));

        [Fact]
        public void GetBytesAfterRelease4() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, new byte[8]));

        [Fact]
        public void GetBytesAfterRelease5() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetBytes(0, new byte[8], 0, 1));

        [Fact]
        public void SetBooleanAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBoolean(0, true));

        [Fact]
        public void SetByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetByte(0, 1));

        [Fact]
        public void SetShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetShort(0, 1));

        [Fact]
        public void SetShortLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetShortLE(0, 1));

        [Fact]
        public void SetMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetMedium(0, 1));

        [Fact]
        public void SetMediumLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetMediumLE(0, 1));

        [Fact]
        public void SetIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetInt(0, 1));

        [Fact]
        public void SetIntLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetIntLE(0, 1));

        [Fact]
        public void SetLongAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetLong(0, 1));

        [Fact]
        public void SetLongLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetLongLE(0, 1));

        [Fact]
        public void SetCharAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetChar(0, (char)1));

        [Fact]
        public void SetFloatAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetFloat(0, 1));

        [Fact]
        public void SetDoubleAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetDouble(0, 1));

        [Fact]
        public void SetBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, this.ReleaseLater(Unpooled.Buffer())));

        [Fact]
        public void SetBytesAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, this.ReleaseLater(Unpooled.Buffer()), 1));

        [Fact]
        public void SetBytesAfterRelease3() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, this.ReleaseLater(Unpooled.Buffer()), 0, 1));

        [Fact]
        public void SetUsAsciiCharSequenceAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.SetCharSequenceAfterRelease0(Encoding.ASCII));

        [Fact]
        public void SetUtf8CharSequenceAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.SetCharSequenceAfterRelease0(Encoding.UTF8));

        [Fact]
        public void SetUtf16CharSequenceAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.SetCharSequenceAfterRelease0(Encoding.Unicode));

        void SetCharSequenceAfterRelease0(Encoding encoding) => this.ReleasedBuffer().SetCharSequence(0, new StringCharSequence("x"), encoding);

        [Fact]
        public void SetUsAsciiStringAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.SetStringAfterRelease0(Encoding.ASCII));

        [Fact]
        public void SetUtf8StringAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.SetStringAfterRelease0(Encoding.UTF8));

        [Fact]
        public void SetUtf16StringAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.SetStringAfterRelease0(Encoding.Unicode));

        void SetStringAfterRelease0(Encoding encoding) => this.ReleasedBuffer().SetString(0, "x", encoding);

        [Fact]
        public void SetBytesAfterRelease4() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, new byte[8]));

        [Fact]
        public void SetBytesAfterRelease5() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetBytes(0, new byte[8], 0, 1));

        [Fact]
        public void SetZeroAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().SetZero(0, 1));

        [Fact]
        public void ReadBooleanAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBoolean());

        [Fact]
        public void ReadByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadByte());

        [Fact]
        public void ReadShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadShort());

        [Fact]
        public void ReadShortLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadShortLE());

        [Fact]
        public void ReadUnsignedShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedShort());

        [Fact]
        public void ReadUnsignedShortLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedShortLE());

        [Fact]
        public void ReadMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadMedium());

        [Fact]
        public void ReadMediumLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadMediumLE());

        [Fact]
        public void ReadUnsignedMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedMedium());

        [Fact]
        public void ReadUnsignedMediumLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedMediumLE());

        [Fact]
        public void ReadIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadInt());

        [Fact]
        public void ReadIntLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadIntLE());

        [Fact]
        public void ReadUnsignedIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedInt());

        [Fact]
        public void ReadUnsignedIntLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadUnsignedIntLE());

        [Fact]
        public void ReadLongAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadLong());

        [Fact]
        public void ReadLongLEEfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadLongLE());

        [Fact]
        public void ReadCharAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadChar());

        [Fact]
        public void ReadFloatAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadFloat());

        [Fact]
        public void ReadDoubleAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadDouble());

        [Fact]
        public void ReadBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(1));

        [Fact]
        public void ReadBytesAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(this.ReleaseLater(Unpooled.Buffer(8))));

        [Fact]
        public void ReadBytesAfterRelease3() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(this.ReleaseLater(Unpooled.Buffer(8))));

        [Fact]
        public void ReadBytesAfterRelease4() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(this.ReleaseLater(Unpooled.Buffer(8)), 0, 1));

        [Fact]
        public void ReadBytesAfterRelease5() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(new byte[8]));

        [Fact]
        public void ReadBytesAfterRelease6() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ReadBytes(new byte[8], 0, 1));

        [Fact]
        public void WriteBooleanAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBoolean(true));

        [Fact]
        public void WriteByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteByte(1));

        [Fact]
        public void WriteShortAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteShort(1));

        [Fact]
        public void WriteShortLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteShortLE(1));

        [Fact]
        public void WriteMediumAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteMedium(1));

        [Fact]
        public void WriteMediumLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteMediumLE(1));

        [Fact]
        public void WriteIntAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteInt(1));

        [Fact]
        public void WriteIntLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteIntLE(1));

        [Fact]
        public void WriteLongAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteLong(1));

        [Fact]
        public void WriteLongLEAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteLongLE(1));

        [Fact]
        public void WriteCharAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteChar((char)1));

        [Fact]
        public void WriteFloatAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteFloat(1));

        [Fact]
        public void WriteDoubleAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteDouble(1));

        [Fact]
        public void WriteBytesAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(this.ReleaseLater(Unpooled.Buffer(8))));

        [Fact]
        public void WriteBytesAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(this.ReleaseLater(Unpooled.CopiedBuffer(new byte[8])), 1));

        [Fact]
        public void WriteBytesAfterRelease3() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(this.ReleaseLater(Unpooled.Buffer(8)), 0, 1));

        [Fact]
        public void WriteBytesAfterRelease4() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(new byte[8]));

        [Fact]
        public void WriteBytesAfterRelease5() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteBytes(new byte[8], 0, 1));

        [Fact]
        public void WriteZeroAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().WriteZero(1));

        [Fact]
        public void WriteUsAsciiCharSequenceAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.WriteCharSequenceAfterRelease0(Encoding.ASCII));

        [Fact]
        public void WriteUtf8CharSequenceAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.WriteCharSequenceAfterRelease0(Encoding.UTF8));

        [Fact]
        public void WriteUtf16CharSequenceAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.WriteCharSequenceAfterRelease0(Encoding.Unicode));

        void WriteCharSequenceAfterRelease0(Encoding encoding) => this.ReleasedBuffer().WriteCharSequence(new StringCharSequence("x"), encoding);

        [Fact]
        public void WriteUsAsciiStringAfterRelease()  => Assert.Throws<IllegalReferenceCountException>(() => this.WriteStringAfterRelease0(Encoding.ASCII));

        [Fact]
        public void WriteUtf8StringAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.WriteStringAfterRelease0(Encoding.UTF8));

        [Fact]
        public void WriteUtf16StringAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.WriteStringAfterRelease0(Encoding.Unicode));

        void WriteStringAfterRelease0(Encoding encoding) => this.ReleasedBuffer().WriteString("x", encoding);

        [Fact]
        public void ForEachByteAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ForEachByte(new TestByteProcessor()));

        [Fact]
        public void ForEachByteAfterRelease1() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ForEachByte(0, 1, new TestByteProcessor()));

        [Fact]
        public void ForEachByteDescAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ForEachByteDesc(new TestByteProcessor()));

        [Fact]
        public void ForEachByteDescAfterRelease1() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().ForEachByteDesc(0, 1, new TestByteProcessor()));

        [Fact]
        public void CopyAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().Copy());

        [Fact]
        public void CopyAfterRelease1() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().Copy());

        [Fact]
        public void IoBufferAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIoBuffer());

        [Fact]
        public void IoBufferAfterRelease1() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIoBuffer(0, 1));

        [Fact]
        public void IoBuffersAfterRelease() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIoBuffers());

        [Fact]
        public void IoBuffersAfterRelease2() => Assert.Throws<IllegalReferenceCountException>(() => this.ReleasedBuffer().GetIoBuffers(0, 1));

        [Fact]
        public void ArrayAfterRelease()
        {
            IByteBuffer buf = this.ReleasedBuffer();
            if (buf.HasArray)
            {
                Assert.Throws<IllegalReferenceCountException>(() =>
                {
                    byte[] _ = buf.Array;
                });
            }
        }

        [Fact]
        public void MemoryAddressAfterRelease()
        {
            IByteBuffer buf = this.ReleasedBuffer();
            if (buf.HasMemoryAddress)
            {
                Assert.Throws<IllegalReferenceCountException>(() => buf.GetPinnableMemoryAddress());
            }
        }

        [Fact]
        public void SliceRelease()
        {
            IByteBuffer buf = this.NewBuffer(8);
            Assert.Equal(1, buf.ReferenceCount);
            Assert.True(buf.Slice().Release());
            Assert.Equal(0, buf.ReferenceCount);
        }

        [Fact]
        public void ReadSliceOutOfBounds() => Assert.Throws<IndexOutOfRangeException>(() => this.ReadSliceOutOfBounds0(false));

        [Fact]
        public void ReadRetainedSliceOutOfBounds() => Assert.Throws<IndexOutOfRangeException>(() => this.ReadSliceOutOfBounds0(true));

        void ReadSliceOutOfBounds0(bool retainedSlice)
        {
            IByteBuffer buf = this.NewBuffer(100);
            try
            {
                buf.WriteZero(50);
                if (retainedSlice)
                {
                    buf.ReadRetainedSlice(51);
                }
                else
                {
                    buf.ReadSlice(51);
                }
            }
            finally
            {
                buf.Release();
            }
        }


        [Fact]
        public virtual void WriteUsAsciiCharSequenceExpand() => this.WriteCharSequenceExpand(Encoding.ASCII);

        [Fact]
        public virtual void WriteUtf8CharSequenceExpand() => this.WriteCharSequenceExpand(Encoding.UTF8);

        [Fact]
        public virtual void WriteUtf16CharSequenceExpand() => this.WriteCharSequenceExpand(Encoding.Unicode);

        void WriteCharSequenceExpand(Encoding encoding)
        {
            IByteBuffer buf = this.NewBuffer(1);
            try
            {
                int writerIndex = buf.Capacity - 1;
                buf.SetWriterIndex(writerIndex);
                int written = buf.WriteCharSequence(new StringCharSequence("AB"), encoding);
                Assert.Equal(writerIndex, buf.WriterIndex - written);
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void SetUsAsciiCharSequenceNoExpand() => Assert.Throws<IndexOutOfRangeException>(() => this.SetCharSequenceNoExpand(Encoding.ASCII));

        [Fact]
        public void SetUtf8CharSequenceNoExpand() => Assert.Throws<IndexOutOfRangeException>(() => this.SetCharSequenceNoExpand(Encoding.UTF8));

        [Fact]
        public void SetUtf16CharSequenceNoExpand() => Assert.Throws<IndexOutOfRangeException>(() => this.SetCharSequenceNoExpand(Encoding.Unicode));

        void SetCharSequenceNoExpand(Encoding encoding)
        {
            IByteBuffer buf = this.NewBuffer(1);
            try
            {
                buf.SetCharSequence(0, new StringCharSequence("AB"), encoding);
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void SetUsAsciiStringNoExpand() => Assert.Throws<IndexOutOfRangeException>(() => this.SetStringNoExpand(Encoding.ASCII));

        [Fact]
        public void SetUtf8StringNoExpand() => Assert.Throws<IndexOutOfRangeException>(() => this.SetStringNoExpand(Encoding.UTF8));

        [Fact]
        public void SetUtf16StringNoExpand() => Assert.Throws<IndexOutOfRangeException>(() => this.SetStringNoExpand(Encoding.Unicode));

        void SetStringNoExpand(Encoding encoding)
        {
            IByteBuffer buf = this.NewBuffer(1);
            try
            {
                buf.SetString(0, "AB", encoding);
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void SetUsAsciiCharSequence() => this.SetGetCharSequence(Encoding.ASCII);

        [Fact]
        public void SetUtf8CharSequence() => this.SetGetCharSequence(Encoding.UTF8);

        [Fact]
        public void SetUtf16CharSequence() => this.SetGetCharSequence(Encoding.Unicode);

        void SetGetCharSequence(Encoding encoding)
        {
            IByteBuffer buf = this.NewBuffer(16);
            var sequence = new StringCharSequence("AB");
            int bytes = buf.SetCharSequence(1, sequence, encoding);
            Assert.Equal(sequence, buf.GetCharSequence(1, bytes, encoding));
            buf.Release();
        }

        [Fact]
        public void SetUsAsciiString() => this.SetGetString(Encoding.ASCII);

        [Fact]
        public void SetUtf8String() => this.SetGetString(Encoding.UTF8);

        [Fact]
        public void SetUtf16String() => this.SetGetString(Encoding.Unicode);

        void SetGetString(Encoding encoding)
        {
            IByteBuffer buf = this.NewBuffer(16);
            const string Sequence = "AB";
            int bytes = buf.SetString(1, Sequence, encoding);
            Assert.Equal(Sequence, buf.GetString(1, bytes, encoding));
            buf.Release();
        }

        [Fact]
        public void WriteReadUsAsciiString() => this.WriteReadString(Encoding.ASCII);

        [Fact]
        public void WriteReadUtf8String() => this.WriteReadString(Encoding.UTF8);

        [Fact]
        public void WriteReadUtf16String() => this.WriteReadString(Encoding.Unicode);

        void WriteReadString(Encoding encoding)
        {
            IByteBuffer buf = this.NewBuffer(16);
            const string Sequence = "AB";
            buf.SetWriterIndex(1);
            int bytes = buf.WriteString(Sequence, encoding);
            buf.SetReaderIndex(1);
            Assert.Equal(Sequence, buf.ReadString(bytes, encoding));
            buf.Release();
        }

        [Fact]
        public void RetainedSliceIndexOutOfBounds() => Assert.Throws<IndexOutOfRangeException>(() => this.SliceOutOfBounds(true, true, true));

        [Fact]
        public void RetainedSliceLengthOutOfBounds() => Assert.Throws<IndexOutOfRangeException>(() => this.SliceOutOfBounds(true, true, false));

        [Fact]
        public void MixedSliceAIndexOutOfBounds() => Assert.Throws<IndexOutOfRangeException>(() => this.SliceOutOfBounds(true, false, false));

        [Fact]
        public void MixedSliceBIndexOutOfBounds() => Assert.Throws<IndexOutOfRangeException>(() => this.SliceOutOfBounds(false, true, false));

        [Fact]
        public void SliceIndexOutOfBounds() => Assert.Throws<IndexOutOfRangeException>(() => this.SliceOutOfBounds(false, false, true));

        [Fact]
        public void SliceLEngthOutOfBounds() => Assert.Throws<IndexOutOfRangeException>(() => this.SliceOutOfBounds(false, false, false));

        [Fact]
        public void RetainedSliceAndRetainedDuplicateContentIsExpected()
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            IByteBuffer expected1 = this.NewBuffer(6).ResetWriterIndex();
            IByteBuffer expected2 = this.NewBuffer(5).ResetWriterIndex();
            IByteBuffer expected3 = this.NewBuffer(4).ResetWriterIndex();
            IByteBuffer expected4 = this.NewBuffer(3).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            expected1.WriteBytes(new byte[] { 2, 3, 4, 5, 6, 7 });
            expected2.WriteBytes(new byte[] { 3, 4, 5, 6, 7 });
            expected3.WriteBytes(new byte[] { 4, 5, 6, 7 });
            expected4.WriteBytes(new byte[] { 5, 6, 7 });

            IByteBuffer slice1 = buf.RetainedSlice(buf.ReaderIndex + 1, 6);
            Assert.Equal(0, slice1.CompareTo(expected1));
            Assert.Equal(0, slice1.CompareTo(buf.Slice(buf.ReaderIndex + 1, 6)));
            // Simulate a handler that releases the original buffer, and propagates a slice.
            buf.Release();

            // Advance the reader index on the slice.
            slice1.ReadByte();

            IByteBuffer dup1 = slice1.RetainedDuplicate();
            Assert.Equal(0, dup1.CompareTo(expected2));
            Assert.Equal(0, dup1.CompareTo(slice1.Duplicate()));

            // Advance the reader index on dup1.
            dup1.ReadByte();

            IByteBuffer dup2 = dup1.Duplicate();
            Assert.Equal(0, dup2.CompareTo(expected3));

            // Advance the reader index on dup2.
            dup2.ReadByte();

            IByteBuffer slice2 = dup2.RetainedSlice(dup2.ReaderIndex, 3);
            Assert.Equal(0, slice2.CompareTo(expected4));
            Assert.Equal(0, slice2.CompareTo(dup2.Slice(dup2.ReaderIndex, 3)));

            // Cleanup the expected buffers used for testing.
            Assert.True(expected1.Release());
            Assert.True(expected2.Release());
            Assert.True(expected3.Release());
            Assert.True(expected4.Release());

            slice2.Release();
            dup2.Release();

            Assert.Equal(slice2.ReferenceCount, dup2.ReferenceCount);
            Assert.Equal(dup2.ReferenceCount, dup1.ReferenceCount);

            // The handler is now done with the original slice
            Assert.True(slice1.Release());

            // Reference counting may be shared, or may be independently tracked, but at this point all buffers should
            // be deallocated and have a reference count of 0.
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, slice1.ReferenceCount);
            Assert.Equal(0, slice2.ReferenceCount);
            Assert.Equal(0, dup1.ReferenceCount);
            Assert.Equal(0, dup2.ReferenceCount);
        }

        [Fact]
        public void RetainedDuplicateAndRetainedSliceContentIsExpected()
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            IByteBuffer expected1 = this.NewBuffer(6).ResetWriterIndex();
            IByteBuffer expected2 = this.NewBuffer(5).ResetWriterIndex();
            IByteBuffer expected3 = this.NewBuffer(4).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            expected1.WriteBytes(new byte[] { 2, 3, 4, 5, 6, 7 });
            expected2.WriteBytes(new byte[] { 3, 4, 5, 6, 7 });
            expected3.WriteBytes(new byte[] { 5, 6, 7 });

            IByteBuffer dup1 = buf.RetainedDuplicate();
            Assert.Equal(0, dup1.CompareTo(buf));
            Assert.Equal(0, dup1.CompareTo(buf.Slice()));
            // Simulate a handler that releases the original buffer, and propagates a slice.
            buf.Release();

            // Advance the reader index on the dup.
            dup1.ReadByte();

            IByteBuffer slice1 = dup1.RetainedSlice(dup1.ReaderIndex, 6);
            Assert.Equal(0, slice1.CompareTo(expected1));
            Assert.Equal(0, slice1.CompareTo(slice1.Duplicate()));

            // Advance the reader index on slice1.
            slice1.ReadByte();

            IByteBuffer dup2 = slice1.Duplicate();
            Assert.Equal(0, dup2.CompareTo(slice1));

            // Advance the reader index on dup2.
            dup2.ReadByte();

            IByteBuffer slice2 = dup2.RetainedSlice(dup2.ReaderIndex + 1, 3);
            Assert.Equal(0, slice2.CompareTo(expected3));
            Assert.Equal(0, slice2.CompareTo(dup2.Slice(dup2.ReaderIndex + 1, 3)));

            // Cleanup the expected buffers used for testing.
            Assert.True(expected1.Release());
            Assert.True(expected2.Release());
            Assert.True(expected3.Release());

            slice2.Release();
            slice1.Release();

            Assert.Equal(slice2.ReferenceCount, dup2.ReferenceCount);
            Assert.Equal(dup2.ReferenceCount, slice1.ReferenceCount);

            // The handler is now done with the original slice
            Assert.True(dup1.Release());

            // Reference counting may be shared, or may be independently tracked, but at this point all buffers should
            // be deallocated and have a reference count of 0.
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, slice1.ReferenceCount);
            Assert.Equal(0, slice2.ReferenceCount);
            Assert.Equal(0, dup1.ReferenceCount);
            Assert.Equal(0, dup2.ReferenceCount);
        }

        [Fact]
        public void RetainedSliceContents() => this.SliceContents0(true);

        [Fact]
        public void MultipleLevelRetainedSlice1() => this.MultipleLevelRetainedSliceWithNonRetained(true, true);

        [Fact]
        public void MultipleLevelRetainedSlice2() => this.MultipleLevelRetainedSliceWithNonRetained(true, false);

        [Fact]
        public void MultipleLevelRetainedSlice3() => this.MultipleLevelRetainedSliceWithNonRetained(false, true);

        [Fact]
        public void MultipleLevelRetainedSlice4() => this.MultipleLevelRetainedSliceWithNonRetained(false, false);

        [Fact]
        public void RetainedSliceReleaseOriginal1() => this.SliceReleaseOriginal(true, true);

        [Fact]
        public void RetainedSliceReleaseOriginal2() => this.SliceReleaseOriginal(true, false);

        [Fact]
        public void RetainedSliceReleaseOriginal3() => this.SliceReleaseOriginal(false, true);

        [Fact]
        public void RetainedSliceReleaseOriginal4() => this.SliceReleaseOriginal(false, false);

        [Fact]
        public void RetainedDuplicateReleaseOriginal1() => this.DuplicateReleaseOriginal(true, true);

        [Fact]
        public void RetainedDuplicateReleaseOriginal2() => this.DuplicateReleaseOriginal(true, false);

        [Fact]
        public void RetainedDuplicateReleaseOriginal3() => this.DuplicateReleaseOriginal(false, true);

        [Fact]
        public void RetainedDuplicateReleaseOriginal4() => this.DuplicateReleaseOriginal(false, false);

        [Fact]
        public void MultipleRetainedSliceReleaseOriginal1() => this.MultipleRetainedSliceReleaseOriginal(true, true);

        [Fact]
        public void MultipleRetainedSliceReleaseOriginal2() => this.MultipleRetainedSliceReleaseOriginal(true, false);

        [Fact]
        public void MultipleRetainedSliceReleaseOriginal3() => this.MultipleRetainedSliceReleaseOriginal(false, true);

        [Fact]
        public void MultipleRetainedSliceReleaseOriginal4() => this.MultipleRetainedSliceReleaseOriginal(false, false);

        [Fact]
        public void MultipleRetainedDuplicateReleaseOriginal1() => this.MultipleRetainedDuplicateReleaseOriginal(true, true);

        [Fact]
        public void MultipleRetainedDuplicateReleaseOriginal2() => this.MultipleRetainedDuplicateReleaseOriginal(true, false);

        [Fact]
        public void MultipleRetainedDuplicateReleaseOriginal3() => this.MultipleRetainedDuplicateReleaseOriginal(false, true);

        [Fact]
        public void MultipleRetainedDuplicateReleaseOriginal4() => this.MultipleRetainedDuplicateReleaseOriginal(false, false);

        [Fact]
        public void SliceContents() => this.SliceContents0(false);

        [Fact]
        public void RetainedDuplicateContents() => this.DuplicateContents0(true);

        [Fact]
        public void DuplicateContents() => this.DuplicateContents0(false);

        [Fact]
        public virtual void DuplicateCapacityChange() => this.DuplicateCapacityChange0(false);

        [Fact]
        public virtual void RetainedDuplicateCapacityChange() => this.DuplicateCapacityChange0(true);

        [Fact]
        public void SliceCapacityChange() => Assert.Throws<NotSupportedException>(() => this.SliceCapacityChange0(false));

        [Fact]
        public void RetainedSliceCapacityChange() => Assert.Throws<NotSupportedException>(() => this.SliceCapacityChange0(true));

        void DuplicateCapacityChange0(bool retainedDuplicate)
        {
            IByteBuffer buf = this.NewBuffer(8);
            IByteBuffer dup = retainedDuplicate ? buf.RetainedDuplicate() : buf.Duplicate();
            try
            {
                dup.AdjustCapacity(10);
                Assert.Equal(buf.Capacity, dup.Capacity);
                dup.AdjustCapacity(5);
                Assert.Equal(buf.Capacity, dup.Capacity);
            }
            finally
            {
                if (retainedDuplicate)
                {
                    dup.Release();
                }
                buf.Release();
            }
        }

        void SliceCapacityChange0(bool retainedSlice)
        {
            IByteBuffer buf = this.NewBuffer(8);
            IByteBuffer slice = retainedSlice ? buf.RetainedSlice(buf.ReaderIndex + 1, 3)
                : buf.Slice(buf.ReaderIndex + 1, 3);
            try
            {
                slice.AdjustCapacity(10);
            }
            finally
            {
                if (retainedSlice)
                {
                    slice.Release();
                }
                buf.Release();
            }
        }

        void SliceOutOfBounds(bool initRetainedSlice, bool finalRetainedSlice, bool indexOutOfBounds)
        {
            IByteBuffer buf = this.NewBuffer(8);
            IByteBuffer slice = initRetainedSlice ? buf.RetainedSlice(buf.ReaderIndex + 1, 2)
                : buf.Slice(buf.ReaderIndex + 1, 2);
            try
            {
                Assert.Equal(2, slice.Capacity);
                Assert.Equal(2, slice.MaxCapacity);
                int index = indexOutOfBounds ? 3 : 0;
                int length = indexOutOfBounds ? 0 : 3;
                if (finalRetainedSlice)
                {
                    // This is expected to fail ... so no need to release.
                    slice.RetainedSlice(index, length);
                }
                else
                {
                    slice.Slice(index, length);
                }
            }
            finally
            {
                if (initRetainedSlice)
                {
                    slice.Release();
                }
                buf.Release();
            }
        }

        void SliceContents0(bool retainedSlice)
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            IByteBuffer expected = this.NewBuffer(3).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            expected.WriteBytes(new byte[] { 4, 5, 6 });
            IByteBuffer slice = retainedSlice ? buf.RetainedSlice(buf.ReaderIndex + 3, 3)
                : buf.Slice(buf.ReaderIndex + 3, 3);
            try
            {
                Assert.Equal(0, slice.CompareTo(expected));
                Assert.Equal(0, slice.CompareTo(slice.Duplicate()));
                IByteBuffer b = slice.RetainedDuplicate();
                Assert.Equal(0, slice.CompareTo(b));
                b.Release();
                Assert.Equal(0, slice.CompareTo(slice.Slice(0, slice.Capacity)));
            }
            finally
            {
                if (retainedSlice)
                {
                    slice.Release();
                }
                buf.Release();
                expected.Release();
            }
        }

        void SliceReleaseOriginal(bool retainedSlice1, bool retainedSlice2)
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            IByteBuffer expected1 = this.NewBuffer(3).ResetWriterIndex();
            IByteBuffer expected2 = this.NewBuffer(2).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            expected1.WriteBytes(new byte[] { 6, 7, 8 });
            expected2.WriteBytes(new byte[] { 7, 8 });
            IByteBuffer slice1 = retainedSlice1 ? buf.RetainedSlice(buf.ReaderIndex + 5, 3)
                : (IByteBuffer) buf.Slice(buf.ReaderIndex + 5, 3).Retain();
            Assert.Equal(0, slice1.CompareTo(expected1));
            // Simulate a handler that releases the original buffer, and propagates a slice.
            buf.Release();

            IByteBuffer slice2 = retainedSlice2 ? slice1.RetainedSlice(slice1.ReaderIndex + 1, 2)
                : (IByteBuffer) slice1.Slice(slice1.ReaderIndex + 1, 2).Retain();
            Assert.Equal(0, slice2.CompareTo(expected2));

            // Cleanup the expected buffers used for testing.
            Assert.True(expected1.Release());
            Assert.True(expected2.Release());

            // The handler created a slice of the slice and is now done with it.
            slice2.Release();

            // The handler is now done with the original slice
            Assert.True(slice1.Release());

            // Reference counting may be shared, or may be independently tracked, but at this point all buffers should
            // be deallocated and have a reference count of 0.
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, slice1.ReferenceCount);
            Assert.Equal(0, slice2.ReferenceCount);
        }

        void MultipleLevelRetainedSliceWithNonRetained(bool doSlice1, bool doSlice2)
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            IByteBuffer expected1 = this.NewBuffer(6).ResetWriterIndex();
            IByteBuffer expected2 = this.NewBuffer(4).ResetWriterIndex();
            IByteBuffer expected3 = this.NewBuffer(2).ResetWriterIndex();
            IByteBuffer expected4SliceSlice = this.NewBuffer(1).ResetWriterIndex();
            IByteBuffer expected4DupSlice = this.NewBuffer(1).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            expected1.WriteBytes(new byte[] { 2, 3, 4, 5, 6, 7 });
            expected2.WriteBytes(new byte[] { 3, 4, 5, 6 });
            expected3.WriteBytes(new byte[] { 4, 5 });
            expected4SliceSlice.WriteBytes(new byte[] { 5 });
            expected4DupSlice.WriteBytes(new byte[] { 4 });

            IByteBuffer slice1 = buf.RetainedSlice(buf.ReaderIndex + 1, 6);
            Assert.Equal(0, slice1.CompareTo(expected1));
            // Simulate a handler that releases the original buffer, and propagates a slice.
            buf.Release();

            IByteBuffer slice2 = slice1.RetainedSlice(slice1.ReaderIndex + 1, 4);
            Assert.Equal(0, slice2.CompareTo(expected2));
            Assert.Equal(0, slice2.CompareTo(slice2.Duplicate()));
            Assert.Equal(0, slice2.CompareTo(slice2.Slice()));

            IByteBuffer tmpBuf = slice2.RetainedDuplicate();
            Assert.Equal(0, slice2.CompareTo(tmpBuf));
            tmpBuf.Release();
            tmpBuf = slice2.RetainedSlice();
            Assert.Equal(0, slice2.CompareTo(tmpBuf));
            tmpBuf.Release();

            IByteBuffer slice3 = doSlice1 ? slice2.Slice(slice2.ReaderIndex + 1, 2) : slice2.Duplicate();
            if (doSlice1)
            {
                Assert.Equal(0, slice3.CompareTo(expected3));
            }
            else
            {
                Assert.Equal(0, slice3.CompareTo(expected2));
            }

            IByteBuffer slice4 = doSlice2 ? slice3.Slice(slice3.ReaderIndex + 1, 1) : slice3.Duplicate();
            if (doSlice1 && doSlice2)
            {
                Assert.Equal(0, slice4.CompareTo(expected4SliceSlice));
            }
            else if (doSlice2)
            {
                Assert.Equal(0, slice4.CompareTo(expected4DupSlice));
            }
            else
            {
                Assert.Equal(0, slice3.CompareTo(slice4));
            }

            // Cleanup the expected buffers used for testing.
            Assert.True(expected1.Release());
            Assert.True(expected2.Release());
            Assert.True(expected3.Release());
            Assert.True(expected4SliceSlice.Release());
            Assert.True(expected4DupSlice.Release());

            // Slice 4, 3, and 2 should effectively "share" a reference count.
            slice4.Release();
            Assert.Equal(slice3.ReferenceCount, slice2.ReferenceCount);
            Assert.Equal(slice3.ReferenceCount, slice4.ReferenceCount);

            // Slice 1 should also release the original underlying buffer without throwing exceptions
            Assert.True(slice1.Release());

            // Reference counting may be shared, or may be independently tracked, but at this point all buffers should
            // be deallocated and have a reference count of 0.
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, slice1.ReferenceCount);
            Assert.Equal(0, slice2.ReferenceCount);
            Assert.Equal(0, slice3.ReferenceCount);
        }

        void DuplicateReleaseOriginal(bool retainedDuplicate1, bool retainedDuplicate2)
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            IByteBuffer expected = this.NewBuffer(8).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            expected.WriteBytes(buf, buf.ReaderIndex, buf.ReadableBytes);
            IByteBuffer dup1 = retainedDuplicate1 ? buf.RetainedDuplicate()
                : (IByteBuffer) buf.Duplicate().Retain();
            Assert.Equal(0, dup1.CompareTo(expected));
            // Simulate a handler that releases the original buffer, and propagates a slice.
            buf.Release();

            IByteBuffer dup2 = retainedDuplicate2 ? dup1.RetainedDuplicate()
                : (IByteBuffer) dup1.Duplicate().Retain();
            Assert.Equal(0, dup2.CompareTo(expected));

            // Cleanup the expected buffers used for testing.
            Assert.True(expected.Release());

            // The handler created a slice of the slice and is now done with it.
            dup2.Release();

            // The handler is now done with the original slice
            Assert.True(dup1.Release());

            // Reference counting may be shared, or may be independently tracked, but at this point all buffers should
            // be deallocated and have a reference count of 0.
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, dup1.ReferenceCount);
            Assert.Equal(0, dup2.ReferenceCount);
        }

        void MultipleRetainedSliceReleaseOriginal(bool retainedSlice1, bool retainedSlice2)
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            IByteBuffer expected1 = this.NewBuffer(3).ResetWriterIndex();
            IByteBuffer expected2 = this.NewBuffer(2).ResetWriterIndex();
            IByteBuffer expected3 = this.NewBuffer(2).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            expected1.WriteBytes(new byte[] { 6, 7, 8 });
            expected2.WriteBytes(new byte[] { 7, 8 });
            expected3.WriteBytes(new byte[] { 6, 7 });
            IByteBuffer slice1 = retainedSlice1 ? buf.RetainedSlice(buf.ReaderIndex + 5, 3)
                : (IByteBuffer)buf.Slice(buf.ReaderIndex + 5, 3).Retain();
            Assert.Equal(0, slice1.CompareTo(expected1));
            // Simulate a handler that releases the original buffer, and propagates a slice.
            buf.Release();

            IByteBuffer slice2 = retainedSlice2 ? slice1.RetainedSlice(slice1.ReaderIndex + 1, 2)
                : (IByteBuffer)slice1.Slice(slice1.ReaderIndex + 1, 2).Retain();
            Assert.Equal(0, slice2.CompareTo(expected2));

            // The handler created a slice of the slice and is now done with it.
            slice2.Release();

            IByteBuffer slice3 = slice1.RetainedSlice(slice1.ReaderIndex, 2);
            Assert.Equal(0, slice3.CompareTo(expected3));

            // The handler created another slice of the slice and is now done with it.
            slice3.Release();

            // The handler is now done with the original slice
            Assert.True(slice1.Release());

            // Cleanup the expected buffers used for testing.
            Assert.True(expected1.Release());
            Assert.True(expected2.Release());
            Assert.True(expected3.Release());

            // Reference counting may be shared, or may be independently tracked, but at this point all buffers should
            // be deallocated and have a reference count of 0.
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, slice1.ReferenceCount);
            Assert.Equal(0, slice2.ReferenceCount);
            Assert.Equal(0, slice3.ReferenceCount);
        }

        void MultipleRetainedDuplicateReleaseOriginal(bool retainedDuplicate1, bool retainedDuplicate2)
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            IByteBuffer expected = this.NewBuffer(8).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            expected.WriteBytes(buf, buf.ReaderIndex, buf.ReadableBytes);
            IByteBuffer dup1 = retainedDuplicate1 ? buf.RetainedDuplicate() : (IByteBuffer)buf.Duplicate().Retain();
            Assert.Equal(0, dup1.CompareTo(expected));
            // Simulate a handler that releases the original buffer, and propagates a slice.
            buf.Release();

            IByteBuffer dup2 = retainedDuplicate2 ? dup1.RetainedDuplicate() : (IByteBuffer)dup1.Duplicate().Retain();
            Assert.Equal(0, dup2.CompareTo(expected));
            Assert.Equal(0, dup2.CompareTo(dup2.Duplicate()));
            Assert.Equal(0, dup2.CompareTo(dup2.Slice()));

            IByteBuffer tmpBuf = dup2.RetainedDuplicate();
            Assert.Equal(0, dup2.CompareTo(tmpBuf));
            tmpBuf.Release();
            tmpBuf = dup2.RetainedSlice();
            Assert.Equal(0, dup2.CompareTo(tmpBuf));
            tmpBuf.Release();

            // The handler created a slice of the slice and is now done with it.
            dup2.Release();

            IByteBuffer dup3 = dup1.RetainedDuplicate();
            Assert.Equal(0, dup3.CompareTo(expected));

            // The handler created another slice of the slice and is now done with it.
            dup3.Release();

            // The handler is now done with the original slice
            Assert.True(dup1.Release());

            // Cleanup the expected buffers used for testing.
            Assert.True(expected.Release());

            // Reference counting may be shared, or may be independently tracked, but at this point all buffers should
            // be deallocated and have a reference count of 0.
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, dup1.ReferenceCount);
            Assert.Equal(0, dup2.ReferenceCount);
            Assert.Equal(0, dup3.ReferenceCount);
        }

        void DuplicateContents0(bool retainedDuplicate)
        {
            IByteBuffer buf = this.NewBuffer(8).ResetWriterIndex();
            buf.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            IByteBuffer dup = retainedDuplicate ? buf.RetainedDuplicate() : buf.Duplicate();
            try
            {
                Assert.Equal(0, dup.CompareTo(buf));
                Assert.Equal(0, dup.CompareTo(dup.Duplicate()));
                IByteBuffer b = dup.RetainedDuplicate();
                Assert.Equal(0, dup.CompareTo(b));
                b.Release();
                Assert.Equal(0, dup.CompareTo(dup.Slice(dup.ReaderIndex, dup.ReadableBytes)));
            }
            finally
            {
                if (retainedDuplicate)
                {
                    dup.Release();
                }
                buf.Release();
            }
        }

        [Fact]
        public void DuplicateRelease()
        {
            IByteBuffer buf = this.NewBuffer(8);
            Assert.Equal(1, buf.ReferenceCount);
            Assert.True(buf.Duplicate().Release());
            Assert.Equal(0, buf.ReferenceCount);
        }

        // Test-case trying to reproduce:
        // https://github.com/netty/netty/issues/2843
        [Fact]
        public void RefCnt() => this.RefCnt0(false);

        // Test-case trying to reproduce:
        // https://github.com/netty/netty/issues/2843
        [Fact]
        public void RefCnt2() => this.RefCnt0(true);

        [Fact]
        public virtual void ReadBytes()
        {
            IByteBuffer buf = this.NewBuffer(8);
            var bytes = new byte[8];
            buf.WriteBytes(bytes);

            IByteBuffer buffer2 = buf.ReadBytes(4);
            Assert.Same(buf.Allocator, buffer2.Allocator);
            Assert.Equal(4, buf.ReaderIndex);
            Assert.True(buf.Release());
            Assert.Equal(0, buf.ReferenceCount);
            Assert.True(buffer2.Release());
            Assert.Equal(0, buffer2.ReferenceCount);
        }

        [Fact]
        public virtual void ForEachByteDesc2()
        {
            byte[] expected = { 1, 2, 3, 4 };
            IByteBuffer buf = this.NewBuffer(expected.Length);

            try
            {
                buf.WriteBytes(expected);
                var processor = new ForEachByteDesc2Processor(expected.Length);
                int i = buf.ForEachByteDesc(processor);
                Assert.Equal(-1, i);
                Assert.True(expected.SequenceEqual(processor.Bytes));
            }
            finally
            {
                buf.Release();
            }
        }

        sealed class ForEachByteDesc2Processor : IByteProcessor
        {
            int index;

            public ForEachByteDesc2Processor(int length)
            {
                this.Bytes = new byte[length];
                this.index = length - 1;
            }

            public byte[] Bytes { get; }

            public bool Process(byte value)
            {
                this.Bytes[this.index--] = value;
                return true;
            }
        }

        [Fact]
        public virtual void ForEachByte2()
        {
            byte[] expected = { 1, 2, 3, 4 };
            IByteBuffer buf = this.NewBuffer(expected.Length);

            try
            {
                buf.WriteBytes(expected);
                var processor = new ForEachByte2Processor(expected.Length);
                int i = buf.ForEachByte(processor);
                Assert.Equal(-1, i);
                Assert.True(expected.SequenceEqual(processor.Bytes));
            }
            finally
            {
                buf.Release();
            }
        }

        sealed class ForEachByte2Processor : IByteProcessor
        {
            int index;

            public ForEachByte2Processor(int length)
            {
                this.Bytes = new byte[length];
                this.index = 0;
            }

            public byte[] Bytes { get; }

            public bool Process(byte value)
            {
                this.Bytes[this.index++] = value;
                return true;
            }
        }

        void RefCnt0(bool parameter)
        {
            for (int i = 0; i < 10; i++)
            {
                var latch = new ManualResetEventSlim();
                var innerLatch = new ManualResetEventSlim();

                IByteBuffer buf = this.NewBuffer(4);
                Assert.Equal(1, buf.ReferenceCount);
                int cnt = int.MaxValue;
                var t1 = new Thread(s =>
                {
                    bool released;
                    if (parameter)
                    {
                        released = buf.Release(buf.ReferenceCount);
                    }
                    else
                    {
                        released = buf.Release();
                    }
                    Assert.True(released);
                    var t2 = new Thread(s2 =>
                    {
                        Volatile.Write(ref cnt, buf.ReferenceCount);
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
        public void EmptyIoBuffers()
        {
            IByteBuffer buf =this.NewBuffer(8);
            buf.Clear();
            Assert.False(buf.IsReadable());
            ArraySegment<byte>[] nioBuffers = buf.GetIoBuffers();
            Assert.Single(nioBuffers);
            Assert.Empty(nioBuffers[0]);
            buf.Release();
        }

        [Fact]
        public void CapacityEnforceMaxCapacity()
        {
            if (this.AssumedMaxCapacity)
            {
                return;
            }

            IByteBuffer buf = this.NewBuffer(3, 13);
            Assert.Equal(13, buf.MaxCapacity);
            Assert.Equal(3, buf.Capacity);
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => buf.AdjustCapacity(14));
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void CapacityNegative()
        {
            if (this.AssumedMaxCapacity)
            {
                return;
            }

            IByteBuffer buf = this.NewBuffer(3, 13);
            Assert.Equal(13, buf.MaxCapacity);
            Assert.Equal(3, buf.Capacity);
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => buf.AdjustCapacity(-1));
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void CapacityDecrease()
        {
            if (this.AssumedMaxCapacity)
            {
                return;
            }

            IByteBuffer buf = this.NewBuffer(3, 13);
            Assert.Equal(13, buf.MaxCapacity);
            Assert.Equal(3, buf.Capacity);
            try
            {
                buf.AdjustCapacity(2);
                Assert.Equal(2, buf.Capacity);
                Assert.Equal(13, buf.MaxCapacity);
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void CapacityIncrease()
        {
            if (this.AssumedMaxCapacity)
            {
                return;
            }

            IByteBuffer buf = this.NewBuffer(3, 13);
            Assert.Equal(13, buf.MaxCapacity);
            Assert.Equal(3, buf.Capacity);
            try
            {
                buf.AdjustCapacity(4);
                Assert.Equal(4, buf.Capacity);
                Assert.Equal(13, buf.MaxCapacity);
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void ReaderIndexLargerThanWriterIndex()
        {
            const string Content1 = "hello";
            const string Content2 = "world";
            int length = Content1.Length + Content2.Length;
            IByteBuffer buf = this.NewBuffer(length);
            buf.SetIndex(0, 0);
            buf.WriteString(Content1, Encoding.ASCII);
            buf.MarkWriterIndex();
            buf.SkipBytes(Content1.Length);
            buf.WriteString(Content2, Encoding.ASCII);
            buf.SkipBytes(Content2.Length);
            Assert.True(buf.ReaderIndex <= buf.WriterIndex);
            try
            {
                Assert.Throws<IndexOutOfRangeException>(() => buf.ResetWriterIndex());
            }
            finally
            {
                buf.Release();
            }
        }

        protected IByteBuffer ReleaseLater(IByteBuffer buf)
        {
            this.buffers.Enqueue(buf);
            return buf;
        }

        sealed class TestByteProcessor : IByteProcessor
        {
            public bool Process(byte value) => true;
        }
    }
}