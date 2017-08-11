// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable UnusedParameter.Local
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public abstract class ByteBufferAllocatorTests
    {
        protected abstract int DefaultMaxCapacity { get; }

        protected abstract int DefaultMaxComponents { get; }

        protected abstract IByteBufferAllocator NewAllocator();

        [Fact]
        public void Buffer()
        {
            IByteBufferAllocator allocator = this.NewAllocator();
            IByteBuffer buffer = allocator.Buffer(1);
            try
            {
                AssertBuffer(buffer, 1, this.DefaultMaxCapacity);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void BufferWithCapacity()
        {
            IByteBufferAllocator allocator = this.NewAllocator();
            IByteBuffer buffer = allocator.Buffer(1, 8);
            try
            {
                AssertBuffer(buffer, 1, 8);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void HeapBuffer()
        {
            IByteBufferAllocator allocator = this.NewAllocator();
            IByteBuffer buffer = allocator.HeapBuffer(1);
            try
            {
                AssertBuffer(buffer, 1, this.DefaultMaxCapacity);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void HeapBufferWithCapacity()
        {
            IByteBufferAllocator allocator = this.NewAllocator();
            IByteBuffer buffer = allocator.HeapBuffer(1, 8);
            try
            {
                AssertBuffer(buffer, 1, 8);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void CompositeBuffer()
        {
            IByteBufferAllocator allocator = this.NewAllocator();
            CompositeByteBuffer buffer = allocator.CompositeBuffer();
            try
            {
                this.AssertCompositeByteBuffer(buffer, this.DefaultMaxComponents);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void CompositeBufferWithCapacity()
        {
            IByteBufferAllocator allocator = this.NewAllocator();
            CompositeByteBuffer buffer = allocator.CompositeBuffer(8);
            try
            {
                this.AssertCompositeByteBuffer(buffer, 8);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void CompositeHeapBuffer()
        {
            IByteBufferAllocator allocator = this.NewAllocator();
            CompositeByteBuffer buffer = allocator.CompositeHeapBuffer();
            try
            {
                this.AssertCompositeByteBuffer(buffer, this.DefaultMaxComponents);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Fact]
        public void CompositeHeapBufferWithCapacity()
        {
            IByteBufferAllocator allocator = this.NewAllocator();
            CompositeByteBuffer buffer = allocator.CompositeHeapBuffer(8);
            try
            {
                this.AssertCompositeByteBuffer(buffer, 8);
            }
            finally
            {
                buffer.Release();
            }
        }

        static void AssertBuffer(IByteBuffer buffer, int expectedCapacity, int expectedMaxCapacity)
        {
            if (!(buffer is CompositeByteBuffer))
            {
                Assert.True(buffer is UnpooledHeapByteBuffer || buffer is PooledHeapByteBuffer,
                    $"Wrong byte buffer type{buffer.GetType().FullName}");
            }

            Assert.Equal(expectedCapacity, buffer.Capacity);
            Assert.Equal(expectedMaxCapacity, buffer.MaxCapacity);
        }

        void AssertCompositeByteBuffer(CompositeByteBuffer buffer, int expectedMaxNumComponents)
        {
            Assert.Equal(0, buffer.NumComponents);
            Assert.Equal(expectedMaxNumComponents, buffer.MaxNumComponents);
            AssertBuffer(buffer, 0, this.DefaultMaxCapacity);
        }
    }
}
