// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench
{
    using System;
    using BenchmarkDotNet.Running;
    using DotNetty.Microbench.Allocators;
    using DotNetty.Microbench.Buffers;
    using DotNetty.Microbench.Codecs;
    using DotNetty.Microbench.Common;
    using DotNetty.Microbench.Concurrency;
    using DotNetty.Microbench.Headers;
    using DotNetty.Microbench.Http;
    using DotNetty.Microbench.Internal;

    class Program
    {
        static readonly Type[] BenchmarkTypes =
        {
            typeof(PooledHeapByteBufferAllocatorBenchmark),
            typeof(UnpooledHeapByteBufferAllocatorBenchmark),

            typeof(ByteBufferBenchmark),
            typeof(PooledByteBufferBenchmark),
            typeof(UnpooledByteBufferBenchmark),
            typeof(PooledByteBufferBenchmark),
            typeof(ByteBufUtilBenchmark),

            typeof(DateFormatterBenchmark),

            typeof(AsciiStringBenchmark),

            typeof(FastThreadLocalBenchmark),
            typeof(SingleThreadEventExecutorBenchmark),

            typeof(HeadersBenchmark),

            typeof(ClientCookieDecoderBenchmark),
            typeof(HttpRequestDecoderBenchmark),
            typeof(HttpRequestEncoderInsertBenchmark),
            typeof(WriteBytesVsShortOrMediumBenchmark),

            typeof(PlatformDependentBenchmark)
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
