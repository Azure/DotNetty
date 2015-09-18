// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using DotNetty.Buffers;

    public class PooledByteBufferTests
    {
        [Fact]
        public void TestPooledByteBufferFactory()
        {
            Assert.Throws<ArgumentException>(() => new PooledByteBufferAllocator(1, 1, 14));
            Assert.Throws<ArgumentException>(() => new PooledByteBufferAllocator(1, 4096, 15));
        }

        [Fact]
        public void TestReadByte()
        {
            var count = 256;
            var factory = this.CreateByteBufferFactory();
            var buffer = factory.Buffer(count);
            var i = 0;
            var bytes = Enumerable.Range(0, count).Select((k) => (byte)k).ToArray();
            buffer.WriteBytes(bytes, 0, count);
            for (i = 0; i < count; i++)
            {
                Assert.Equal((byte)i, buffer.ReadByte());
            }
            Assert.Equal(0, buffer.ReadableBytes);
        }

        [Fact]
        public void TestWriteBytes()
        {
            var factory = this.CreateByteBufferFactory();
            var buffer = factory.Buffer(1024);
            var totals = 0;
            for (var i = 0; i < 1024; i++)
            {
                var bytes = new byte[i << 2];
                buffer.WriteBytes(bytes, 0, bytes.Length);
                totals += i << 2;
            }
            Assert.Equal(totals, buffer.ReadableBytes);
        }

        [Fact]
        public void TestReadBytes()
        {
            var factory = this.CreateByteBufferFactory();
            var buffer = factory.Buffer(1024);
            var bytes = Enumerable.Range(0, 256).Select((k) => (byte)k).ToArray();
            buffer.WriteBytes(bytes, 0, bytes.Length);
            Assert.Equal(768, buffer.WritableBytes);
            var copyBytes = new byte[256];
            buffer.ReadBytes(copyBytes, 10, 200);
            Assert.Equal(200, buffer.ReaderIndex);
            Assert.Equal((byte)200, buffer.ReadByte());
        }

        [Fact]
        public void TestConcurrent()
        {
            var factory = this.CreateByteBufferFactory();
            var tasks = new Task[5];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    var loop = 0;
                    while (loop++ < 10)
                    {
                        var buffer = factory.Buffer(12);
                        var bytes = Encoding.UTF8.GetBytes("hello,world.");
                        buffer.WriteBytes(bytes, 0, bytes.Length);
                        var dst = new byte[bytes.Length];
                        buffer.ReadBytes(dst, 0, dst.Length);
                        Assert.Equal(bytes[0], dst[0]);
                        Assert.Equal(bytes[11], dst[11]);
                        buffer.Release();
                    }
                });
            }
            Task.WaitAll(tasks);
        }

        private IByteBufferAllocator CreateByteBufferFactory()
        {
            return new PooledByteBufferAllocator();
        }
    }
}
