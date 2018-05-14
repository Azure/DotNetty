// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Http
{
    using System.Text;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using BenchmarkDotNet.Engines;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common;
    using DotNetty.Transport.Channels.Embedded;

    [SimpleJob(RunStrategy.Monitoring, 10, 5, 20)]
    [BenchmarkCategory("Http")]
    public class HttpRequestDecoderBenchmark
    {
        const int ContentLength = 120;

        [Params(2, 4, 8, 16, 32)]
        public int Step { get; set; }

        byte[] contentMixedDelimiters;

        [GlobalSetup]
        public void GlobalSetup()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
            this.contentMixedDelimiters = CreateContent("\r\n", "\n");
        }

        [Benchmark]
        public void DecodeWholeRequestInMultipleStepsMixedDelimiters() => 
            DecodeWholeRequestInMultipleSteps(this.contentMixedDelimiters, this.Step);

        static void DecodeWholeRequestInMultipleSteps(byte[] content, int fragmentSize)
        {
            var channel = new EmbeddedChannel(new HttpRequestDecoder());

            int headerLength = content.Length - ContentLength;

            // split up the header
            for (int a = 0; a < headerLength;)
            {
                int amount = fragmentSize;
                if (a + amount > headerLength)
                {
                    amount = headerLength - a;
                }

                // if header is done it should produce a HttpRequest
                channel.WriteInbound(Unpooled.WrappedBuffer(content, a, amount));
                a += amount;
            }

            for (int i = ContentLength; i > 0; i--)
            {
                // Should produce HttpContent
                channel.WriteInbound(Unpooled.WrappedBuffer(content, content.Length - i, 1));
            }
        }

        static byte[] CreateContent(params string[] lineDelimiters)
        {
            string lineDelimiter;
            string lineDelimiter2;
            if (lineDelimiters.Length == 2)
            {
                lineDelimiter = lineDelimiters[0];
                lineDelimiter2 = lineDelimiters[1];
            }
            else
            {
                lineDelimiter = lineDelimiters[0];
                lineDelimiter2 = lineDelimiters[0];
            }
            // This GET request is incorrect but it does not matter for HttpRequestDecoder.
            // It used only to get a long request.
            return Encoding.ASCII.GetBytes("GET /some/path?foo=bar&wibble=eek HTTP/1.1" + "\r\n" +
                "Upgrade: WebSocket" + lineDelimiter2 +
                "Connection: Upgrade" + lineDelimiter +
                "Host: localhost" + lineDelimiter2 +
                "Referer: http://www.site.ru/index.html" + lineDelimiter +
                "User-Agent: Mozilla/5.0 (X11; U; Linux i686; ru; rv:1.9b5) Gecko/2008050509 Firefox/3.0b5" +
                lineDelimiter2 +
                "Accept: text/html" + lineDelimiter +
                "Cookie: income=1" + lineDelimiter2 +
                "Origin: http://localhost:8080" + lineDelimiter +
                "Sec-WebSocket-Key1: 10  28 8V7 8 48     0" + lineDelimiter2 +
                "Sec-WebSocket-Key2: 8 Xt754O3Q3QW 0   _60" + lineDelimiter +
                "Content-Type: application/x-www-form-urlencoded" + lineDelimiter2 +
                "Content-Length: " + ContentLength + lineDelimiter +
                "\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n" +
                "1234567890\r\n"
            );
        }
    }
}
