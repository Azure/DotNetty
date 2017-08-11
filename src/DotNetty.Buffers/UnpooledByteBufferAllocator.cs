// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Threading;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     Unpooled implementation of <see cref="IByteBufferAllocator" />.
    /// </summary>
    public sealed class UnpooledByteBufferAllocator : AbstractByteBufferAllocator, IByteBufferAllocatorMetricProvider
    {
        readonly UnpooledByteBufferAllocatorMetric metric = new UnpooledByteBufferAllocatorMetric();
        readonly bool disableLeakDetector;

        public static readonly UnpooledByteBufferAllocator Default = new UnpooledByteBufferAllocator();

        public UnpooledByteBufferAllocator(bool disableLeakDetector = false)
        {
            this.disableLeakDetector = disableLeakDetector;
        }

        protected override IByteBuffer NewHeapBuffer(int initialCapacity, int maxCapacity) =>
            new InstrumentedUnpooledHeapByteBuffer(this, initialCapacity, maxCapacity);

        public override CompositeByteBuffer CompositeHeapBuffer(int maxNumComponents)
        {
            var buf = new CompositeByteBuffer(this, maxNumComponents);
            return this.disableLeakDetector ? buf : ToLeakAwareBuffer(buf);
        }

        public IByteBufferAllocatorMetric Metric => this.metric;

        internal void IncrementHeap(int amount) => this.metric.HeapCounter(amount);
        
        internal void DecrementHeap(int amount) => this.metric.HeapCounter(-amount);

        sealed class InstrumentedUnpooledHeapByteBuffer : UnpooledHeapByteBuffer
        {
            internal InstrumentedUnpooledHeapByteBuffer(
                UnpooledByteBufferAllocator alloc, int initialCapacity, int maxCapacity)
                : base(alloc, initialCapacity, maxCapacity)
            {
                ((UnpooledByteBufferAllocator)this.Allocator).IncrementHeap(initialCapacity);
            }

            protected override byte[] AllocateArray(int initialCapacity)
            {
                byte[] bytes = base.AllocateArray(initialCapacity);
                ((UnpooledByteBufferAllocator)this.Allocator).IncrementHeap(bytes.Length);
                return bytes;
            }

            protected override void FreeArray(byte[] bytes)
            {
                int length = bytes.Length;
                base.FreeArray(bytes);
                ((UnpooledByteBufferAllocator)this.Allocator).DecrementHeap(length);
            }
        }

        sealed class UnpooledByteBufferAllocatorMetric : IByteBufferAllocatorMetric
        {
            long usedHeapMemory;

            public long UsedHeapMemory => Volatile.Read(ref this.usedHeapMemory);

            public void HeapCounter(int amount) => Interlocked.Add(ref this.usedHeapMemory, amount);

            public override string ToString() => $"{StringUtil.SimpleClassName(this)} (usedHeapMemory: {this.UsedHeapMemory})";
        }
    }
}