// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Codecs
{
    using System;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using DotNetty.Codecs;

    [CoreJob]
    [BenchmarkCategory("Codecs")]
    public class DateFormatterBenchmark
    {
        const string DateString = "Sun, 27 Nov 2016 19:18:46 GMT";
        readonly DateTime date = new DateTime(784111777000L);

        [Benchmark]
        public DateTime? ParseHttpHeaderDateFormatter() => DateFormatter.ParseHttpDate(DateString);

        [Benchmark]
        public string FormatHttpHeaderDateFormatter() => DateFormatter.Format(this.date);
    }
}
