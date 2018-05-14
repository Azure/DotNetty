// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Internal
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using DotNetty.Common.Internal;

    [CoreJob]
    [BenchmarkCategory("Internal")]
    public class PlatformDependentBenchmark
    {
        [Params(10, 50, 100, 1000, 10000, 100000)]
        public int Size { get; set; }

        byte[] bytes1;
        byte[] bytes2;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.bytes1 = new byte[this.Size];
            this.bytes2 = new byte[this.Size];
            for (int i = 0; i < this.Size; i++)
            {
                this.bytes1[i] = this.bytes2[i] = (byte)i;
            }
        }

        [Benchmark]
        public bool UnsafeBytesEqual() =>
            PlatformDependent.ByteArrayEquals(this.bytes1, 0, this.bytes2, 0, this.bytes1.Length);
    }
}
