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

        protected abstract IByteBufferAllocator NewAllocator(bool preferDirect);

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Buffer(bool preferDirect)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            IByteBuffer buffer = allocator.Buffer(1);
            try
            {
                AssertBuffer(buffer, this.IsDirectExpected(preferDirect), 1, this.DefaultMaxCapacity);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Theory]
        [InlineData(true, 8)]
        [InlineData(false, 8)]
        public void BufferWithCapacity(bool preferDirect, int maxCapacity)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            IByteBuffer buffer = allocator.Buffer(1, maxCapacity);
            try
            {
                AssertBuffer(buffer, this.IsDirectExpected(preferDirect), 1, maxCapacity);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void HeapBuffer(bool preferDirect)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            IByteBuffer buffer = allocator.HeapBuffer(1);
            try
            {
                AssertBuffer(buffer, false, 1, this.DefaultMaxCapacity);
            }
            finally
            {
                buffer.Release();
            }
        }

        protected abstract bool IsDirectExpected(bool preferDirect);

        [Theory]
        [InlineData(true, 8)]
        [InlineData(false, 8)]
        public void HeapBufferWithCapacity(bool preferDirect, int maxCapacity)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            IByteBuffer buffer = allocator.HeapBuffer(1, maxCapacity);
            try
            {
                AssertBuffer(buffer, false, 1, maxCapacity);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DirectBuffer(bool preferDirect)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            IByteBuffer buffer = allocator.DirectBuffer(1);
            try
            {
                AssertBuffer(buffer, true, 1, this.DefaultMaxCapacity);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Theory]
        [InlineData(true, 8)]
        [InlineData(false, 8)]
        public void DirectBufferWithCapacity(bool preferDirect, int maxCapacity)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            IByteBuffer buffer = allocator.DirectBuffer(1, maxCapacity);
            try
            {
                AssertBuffer(buffer, true, 1, maxCapacity);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CompositeBuffer(bool preferDirect)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
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

        [Theory]
        [InlineData(true, 8)]
        [InlineData(false, 8)]
        public void CompositeBufferWithCapacity(bool preferDirect, int maxNumComponents) => this.TestCompositeHeapBufferWithCapacity(preferDirect, maxNumComponents);

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CompositeHeapBuffer(bool preferDirect)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
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

        [Theory]
        [InlineData(true, 8)]
        [InlineData(false, 8)]
        public void CompositeHeapBufferWithCapacity(bool preferDirect, int maxNumComponents) => this.TestCompositeHeapBufferWithCapacity(preferDirect, maxNumComponents);

        void TestCompositeHeapBufferWithCapacity(bool preferDirect, int maxNumComponents)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            CompositeByteBuffer buffer = allocator.CompositeHeapBuffer(maxNumComponents);
            try
            {
                this.AssertCompositeByteBuffer(buffer, maxNumComponents);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CompositeDirectBuffer(bool preferDirect)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            CompositeByteBuffer buffer = allocator.CompositeDirectBuffer();
            try
            {
                this.AssertCompositeByteBuffer(buffer, this.DefaultMaxComponents);
            }
            finally
            {
                buffer.Release();
            }
        }

        [Theory]
        [InlineData(true, 8)]
        [InlineData(false, 8)]
        public void CompositeDirectBufferWithCapacity(bool preferDirect, int maxNumComponents)
        {
            IByteBufferAllocator allocator = this.NewAllocator(preferDirect);
            CompositeByteBuffer buffer = allocator.CompositeDirectBuffer(maxNumComponents);
            try
            {
                this.AssertCompositeByteBuffer(buffer, maxNumComponents);
            }
            finally
            {
                buffer.Release();
            }
        }

        static void AssertBuffer(IByteBuffer buffer, bool expectedDirect, int expectedCapacity, int expectedMaxCapacity)
        {
            Assert.Equal(expectedDirect, buffer.IsDirect);
            Assert.Equal(expectedCapacity, buffer.Capacity);
            Assert.Equal(expectedMaxCapacity, buffer.MaxCapacity);
        }

        void AssertCompositeByteBuffer(CompositeByteBuffer buffer, int expectedMaxNumComponents)
        {
            Assert.Equal(0, buffer.NumComponents);
            Assert.Equal(expectedMaxNumComponents, buffer.MaxNumComponents);
            AssertBuffer(buffer, false, 0, this.DefaultMaxCapacity);
        }
    }
}
