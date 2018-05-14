// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Headers
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using BenchmarkDotNet.Engines;
    using DotNetty.Codecs;
    using DotNetty.Codecs.Http;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    [SimpleJob(RunStrategy.Monitoring, 10, 5, 10)]
    [BenchmarkCategory("Headers")]
    public class HeadersBenchmark
    {
        [Params(3, 5, 6, 8, 11, 22, 30)]
        public int HeaderSize { get; set; }

        AsciiString[] httpNames;
        AsciiString[] httpValues;

        DefaultHttpHeaders httpHeaders;
        DefaultHttpHeaders emptyHttpHeaders;
        DefaultHttpHeaders emptyHttpHeadersNoValidate;

        static string ToHttpName(string name) => name.StartsWith(":") ? name.Substring(1) : name;

        [GlobalSetup]
        public void GlobalSetup()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
            Dictionary<HeaderExample, Dictionary<string, string>> headersSet = ExampleHeaders.GetExamples();
            Dictionary<string, string> headers = headersSet[(HeaderExample)this.HeaderSize];
            this.httpNames = new AsciiString[headers.Count];
            this.httpValues = new AsciiString[headers.Count];
            this.httpHeaders = new DefaultHttpHeaders(false);
            int idx = 0;
            foreach (KeyValuePair<string, string> header in headers)
            {
                string httpName = ToHttpName(header.Key);
                string value = header.Value;
                this.httpNames[idx] = new AsciiString(httpName);
                this.httpValues[idx] = new AsciiString(value);
                this.httpHeaders.Add(this.httpNames[idx], this.httpValues[idx]);
                idx++;
            }
            this.emptyHttpHeaders = new DefaultHttpHeaders();
            this.emptyHttpHeadersNoValidate = new DefaultHttpHeaders(false);
        }

        [Benchmark]
        public DefaultHttpHeaders HttpRemove()
        {
            foreach(AsciiString name in this.httpNames)
            {
                this.httpHeaders.Remove(name);
            }

            return this.httpHeaders;
        }

        [Benchmark]
        public DefaultHttpHeaders HttpGet()
        {
            foreach (AsciiString name in this.httpNames)
            {
                this.httpHeaders.TryGet(name, out _);
            }
            return this.httpHeaders;
        }

        [Benchmark]
        public DefaultHttpHeaders HttpPut()
        {
            var headers = new DefaultHttpHeaders(false);
            for (int i = 0; i < this.httpNames.Length; i++)
            {
                headers.Add(this.httpNames[i], this.httpValues[i]);
            }
            return headers;
        }

        [Benchmark]
        public List<HeaderEntry<AsciiString, ICharSequence>> HttpIterate()
        {
            var list = new List<HeaderEntry<AsciiString, ICharSequence>>();
            foreach (HeaderEntry<AsciiString, ICharSequence> header in this.httpHeaders)
            {
                list.Add(header);
            }
            return list;
        }

        [Benchmark]
        public DefaultHttpHeaders HttpAddAllFastest()
        {
            this.emptyHttpHeadersNoValidate.Add(this.httpHeaders);
            return this.emptyHttpHeadersNoValidate;
        }

        [Benchmark]
        public DefaultHttpHeaders HttpAddAllFast()
        {
            this.emptyHttpHeaders.Add(this.httpHeaders);
            return this.emptyHttpHeaders;
        }
    }
}
