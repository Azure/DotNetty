// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Buffers
{
    using System;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Diagnostics.Windows.Configs;
    using BenchmarkDotNet.Jobs;
    using DotNetty.Buffers;
    using DotNetty.Common;
#if NET46
    using BenchmarkDotNet.Diagnostics.Windows.Configs;
#endif

#if !NET472
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
#else
    [SimpleJob(RuntimeMoniker.Net472)]
    [InliningDiagnoser(true, true)]
#endif
    [BenchmarkCategory("ByteBuffer")]
    public class ByteBufferBenchmark
    {
        const string PropMode = "io.netty.buffer.bytebuf.checkAccessible";

        static ByteBufferBenchmark()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
            Environment.SetEnvironmentVariable(PropMode, "false");
        }

        readonly IByteBuffer unpooledBuffer;
        readonly IByteBuffer pooledBuffer;

        public ByteBufferBenchmark()
        {
            var unpooled = new UnpooledByteBufferAllocator(true);
            this.unpooledBuffer = unpooled.Buffer(8);
            this.pooledBuffer = PooledByteBufferAllocator.Default.Buffer(8);
        }

        [Benchmark]
        public void SetByteUnpooled() => this.unpooledBuffer.SetByte(0, 0);

        [Benchmark]
        public void SetBytePooled() =>this.pooledBuffer.SetByte(0, 0);

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.unpooledBuffer.Release();
            this.pooledBuffer.Release();
        }
    }
}
