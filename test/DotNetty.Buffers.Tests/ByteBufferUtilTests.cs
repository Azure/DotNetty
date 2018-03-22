// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using Xunit;

    public class ByteBufferUtilTests
    {
        [Fact]
        public void EqualsBufferSubsections()
        {
            var b1 = new byte[128];
            var b2 = new byte[256];
            var rand = new Random();
            rand.NextBytes(b1);
            rand.NextBytes(b2);
            int iB1 = b1.Length / 2;
            int iB2 = iB1 + b1.Length;
            int length = b1.Length - iB1;
            Array.Copy(b1, iB1, b2, iB2, length);
            Assert.True(ByteBufferUtil.Equals(Unpooled.WrappedBuffer(b1), iB1, Unpooled.WrappedBuffer(b2), iB2, length));
        }

        static int GetRandom(Random r, int min, int max) =>  r.Next((max - min) + 1) + min;

        [Fact]
        public void NotEqualsBufferSubsections()
        {
            var b1 = new byte[50];
            var b2 = new byte[256];
            var rand = new Random();
            rand.NextBytes(b1);
            rand.NextBytes(b2);
            int iB1 = b1.Length / 2;
            int iB2 = iB1 + b1.Length;
            int length = b1.Length - iB1;

            Array.Copy(b1, iB1, b2, iB2, length);
            // Randomly pick an index in the range that will be compared and make the value at that index differ between
            // the 2 arrays.
            int diffIndex = GetRandom(rand, iB1, iB1 + length - 1);
            ++b1[diffIndex];
            Assert.False(ByteBufferUtil.Equals(Unpooled.WrappedBuffer(b1), iB1, Unpooled.WrappedBuffer(b2), iB2, length));
        }

        [Fact]
        public void NotEqualsBufferOverflow()
        {
            var b1 = new byte[8];
            var b2 = new byte[16];
            var rand = new Random();
            rand.NextBytes(b1);
            rand.NextBytes(b2);
            int iB1 = b1.Length / 2;
            int iB2 = iB1 + b1.Length;
            int length = b1.Length - iB1;
            Array.Copy(b1, iB1, b2, iB2, length - 1);
            Assert.False(ByteBufferUtil.Equals(Unpooled.WrappedBuffer(b1), iB1, Unpooled.WrappedBuffer(b2), iB2,
                Math.Max(b1.Length, b2.Length) * 2));
        }

        [Fact]
        public void NotEqualsBufferUnderflow()
        {
            var b1 = new byte[8];
            var b2 = new byte[16];
            var rand = new Random();
            rand.NextBytes(b1);
            rand.NextBytes(b2);
            int iB1 = b1.Length / 2;
            int iB2 = iB1 + b1.Length;
            int length = b1.Length - iB1;
            Array.Copy(b1, iB1, b2, iB2, length - 1);
            Assert.Throws<ArgumentException>(() => ByteBufferUtil.Equals(Unpooled.WrappedBuffer(b1), iB1, Unpooled.WrappedBuffer(b2), iB2, -1));
        }
    }
}
