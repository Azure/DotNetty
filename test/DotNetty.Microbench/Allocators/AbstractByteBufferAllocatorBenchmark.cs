// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Allocators
{
    using System;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using DotNetty.Buffers;
    using DotNetty.Common;

    [CoreJob]
    [BenchmarkCategory("ByteBufferAllocator")]
    [MemoryDiagnoser]
    public abstract class AbstractByteBufferAllocatorBenchmark
    {
        const int MaxLiveBuffers = 8192;
        public const int IterationCount = 3;

        readonly IByteBufferAllocator allocator;

        readonly Random rand = new Random();
        readonly IByteBuffer[] buffers = new IByteBuffer[MaxLiveBuffers];

        static AbstractByteBufferAllocatorBenchmark()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
        }

        protected AbstractByteBufferAllocatorBenchmark(IByteBufferAllocator allocator)
        {
            this.allocator = allocator;
        }

        [Params(0, 256, 1024, 4096, 16384, 65536)]
        public int Size { get; set; }

        [Benchmark]
        public void Allocate() => this.AllocateAndFree(this.Size);

        void AllocateAndFree(int size)
        {
            int idx = this.rand.Next(this.buffers.Length);
            IByteBuffer oldBuf = this.buffers[idx];
            oldBuf?.Release();
            this.buffers[idx] = this.allocator.Buffer(size);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            for (int i = 0; i < this.buffers.Length; i++)
            {
                this.buffers[i]?.Release();
                this.buffers[i] = null;
            }
        }
    }
}
