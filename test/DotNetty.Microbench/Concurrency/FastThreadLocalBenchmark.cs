// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Concurrency
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using DotNetty.Common;

    [CoreJob]
    [BenchmarkCategory("Concurrency")]
    public class FastThreadLocalBenchmark
    {
        ThreadLocalArray threadLocalArray;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.threadLocalArray = new ThreadLocalArray(new byte[128]);
        }

        [Benchmark]
        public byte[] Get() => this.threadLocalArray.Value;

        [GlobalCleanup]
        public void GlobalCleanup() => FastThreadLocal.Destroy();

        sealed class ThreadLocalArray : FastThreadLocal<byte[]>
        {
            readonly byte[] array;

            public ThreadLocalArray(byte[] array)
            {
                this.array = array;
            }

            protected override byte[] GetInitialValue() => this.array;
        }
    }
}
