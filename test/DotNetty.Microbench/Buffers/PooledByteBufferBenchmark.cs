// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Buffers
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using DotNetty.Buffers;
    using DotNetty.Common;
#if NET46
    using BenchmarkDotNet.Diagnostics.Windows.Configs;
#endif

#if !NET46
    [CoreJob]
#else
    [ClrJob]
    [InliningDiagnoser]
#endif
    [BenchmarkCategory("ByteBuffer")]
    public class PooledByteBufferBenchmark
    {
        static PooledByteBufferBenchmark()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
        }

        AbstractByteBuffer unsafeBuffer;
        AbstractByteBuffer buffer;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.unsafeBuffer = (AbstractByteBuffer)PooledByteBufferAllocator.Default.DirectBuffer(8);
            this.buffer = (AbstractByteBuffer)PooledByteBufferAllocator.Default.HeapBuffer(8);
            this.buffer.WriteLong(1L);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.unsafeBuffer.Release();
            this.buffer.Release();
        }

        [Benchmark]
        public void CheckIndexUnsafe() => this.unsafeBuffer.CheckIndex(0, 8);

        [Benchmark]
        public void CheckIndex() => this.buffer.CheckIndex(0, 8);

        [Benchmark]
        public byte GetByteUnsafe() => this.unsafeBuffer.GetByte(0);

        [Benchmark]
        public byte GetByte() => this.buffer.GetByte(0);

        [Benchmark]
        public short GetShortUnsafe() => this.unsafeBuffer.GetShort(0);

        [Benchmark]
        public short GetShort() => this.buffer.GetShort(0);

        [Benchmark]
        public int GetMediumUnsafe() => this.unsafeBuffer.GetMedium(0);

        [Benchmark]
        public int GetMedium() => this.buffer.GetMedium(0);

        [Benchmark]
        public int GetIntUnsafe() => this.unsafeBuffer.GetInt(0);

        [Benchmark]
        public int GetInt() => this.buffer.GetInt(0);

        [Benchmark]
        public long GetLongUnsafe() => this.unsafeBuffer.GetLong(0);

        [Benchmark]
        public long GetLong() => this.buffer.GetLong(0);
    }
}