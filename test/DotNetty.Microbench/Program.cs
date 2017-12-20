// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench
{
    using System;
    using BenchmarkDotNet.Running;
    using DotNetty.Microbench.Allocators;
    using DotNetty.Microbench.Buffers;
    using DotNetty.Microbench.Concurrency;

    class Program
    {
        static readonly Type[] BenchmarkTypes =
        {
            typeof(PooledByteBufferAllocatorBenchmark),
            typeof(UnpooledByteBufferAllocatorBenchmark),
            typeof(ByteBufferBenchmark),
            typeof(UnpooledByteBufferBenchmark),
            typeof(PooledByteBufferBenchmark),
            typeof(FastThreadLocalBenchmark),
            typeof(SingleThreadEventExecutorBenchmark)
        };

        static void Main(string[] args)
        {
            var switcher = new BenchmarkSwitcher(BenchmarkTypes);

            if (args == null || args.Length == 0)
            {
                switcher.RunAll();
            }
            else
            {
                switcher.Run(args);
            }
        }
    }
}
