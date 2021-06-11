// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Common
{
    using System;
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Jobs;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
#if NET472
    using BenchmarkDotNet.Diagnostics.Windows.Configs;
#endif

#if !NET472
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
#else
    [SimpleJob(RuntimeMoniker.Net472)]
    [InliningDiagnoser(true, true)]
#endif
    [BenchmarkCategory("Common")]
    public class AsciiStringBenchmark
    {
        [Params(3, 5, 7, 8, 10, 20, 50, 100, 1000)]
        public int Size { get; set; }

        AsciiString asciiString;
        StringCharSequence stringValue;
        static readonly Random RandomGenerator = new Random();

        [GlobalSetup]
        public void GlobalSetup()
        {
            var bytes = new byte[this.Size];
            RandomGenerator.NextBytes(bytes);

            this.asciiString = new AsciiString(bytes, false);
            string value = Encoding.ASCII.GetString(bytes);
            this.stringValue = new StringCharSequence(value);
        }

        [Benchmark]
        public int CharSequenceHashCode() => PlatformDependent.HashCodeAscii(this.stringValue);

        [Benchmark]
        public int AsciiStringHashCode() => PlatformDependent.HashCodeAscii(
            this.asciiString.Array, this.asciiString.Offset, this.asciiString.Count);
    }
}
