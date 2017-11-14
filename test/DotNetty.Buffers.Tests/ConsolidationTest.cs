// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System.Text;
    using Xunit;

    using static Unpooled;

    public class ConsolidationTest
    {
        [Fact]
        public void ShouldWrapInSequence()
        {
            IByteBuffer currentBuffer = WrappedBuffer(WrappedBuffer(Encoding.ASCII.GetBytes("a")),
                WrappedBuffer(Encoding.ASCII.GetBytes("=")));
            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("1")),
                WrappedBuffer(Encoding.ASCII.GetBytes("&")));

            IByteBuffer copy = currentBuffer.Copy();
            string s = copy.ToString(Encoding.ASCII);
            Assert.Equal("a=1&", s);

            currentBuffer.Release();
            copy.Release();
        }

        [Fact]
        public void ShouldConsolidationInSequence()
        {
            IByteBuffer currentBuffer = WrappedBuffer(WrappedBuffer(Encoding.ASCII.GetBytes("a")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("=")));
            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("1")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("&")));

            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("b")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("=")));
            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("2")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("&")));

            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("c")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("=")));
            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("3")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("&")));

            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("d")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("=")));
            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("4")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("&")));

            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("e")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("=")));
            currentBuffer = WrappedBuffer(currentBuffer, WrappedBuffer(Encoding.ASCII.GetBytes("5")),
                    WrappedBuffer(Encoding.ASCII.GetBytes("&")));

            IByteBuffer copy = currentBuffer.Copy();
            string s = copy.ToString(Encoding.ASCII);
            Assert.Equal("a=1&b=2&c=3&d=4&e=5&", s);

            currentBuffer.Release();
            copy.Release();
        }
    }
}
