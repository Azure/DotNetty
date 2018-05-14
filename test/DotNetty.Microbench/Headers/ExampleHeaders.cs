// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Headers
{
    using System.Collections.Generic;

    public enum HeaderExample
    {
        Three = 3,
        Five = 5,
        Six = 6,
        Eight = 8,
        Eleven = 11,
        Twentytwo = 22,
        Thirty = 30
    }

    static class ExampleHeaders
    {
        public static Dictionary<HeaderExample, Dictionary<string, string>> GetExamples()
        {
            var examples = new Dictionary<HeaderExample, Dictionary<string, string>>();

            var header = new Dictionary<string, string>
            {
                { ":method", "GET" },
                { ":scheme", "https" },
                { ":path", "/index.html" }
            };
            examples.Add(HeaderExample.Three, header);

            // Headers used by Norman's HTTP benchmarks with wrk
            header = new Dictionary<string, string>
            {
                { "Method", "GET" },
                { "Path", "/plaintext" },
                { "Host", "localhost" },
                { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                { "Connection", "keep-alive" }
            };
            examples.Add(HeaderExample.Five, header);

            header = new Dictionary<string, string>
            {
                { ":authority", "127.0.0.1:33333" },
                { ":method", "POST" },
                { ":path", "/grpc.testing.TestService/UnaryCall" },
                { ":scheme", "http" },
                { "content-type", "application/grpc" },
                { "te", "trailers" }
            };
            examples.Add(HeaderExample.Six, header);

            header = new Dictionary<string, string>
            {
                { ":method", "POST" },
                { ":scheme", "http" },
                { ":path", "/google.pubsub.v2.PublisherService/CreateTopic" },
                { ":authority", "pubsub.googleapis.com" },
                { "grpc-timeout", "1S" },
                { "content-type", "application/grpc+proto" },
                { "grpc-encoding", "gzip" },
                { "authorization", "Bearer y235.wef315yfh138vh31hv93hv8h3v" }
            };
            examples.Add(HeaderExample.Eight, header);

            header = new Dictionary<string, string>
            {
                { ":host", "twitter.com" },
                { ":method", "GET" },
                { ":path", "/" },
                { ":scheme", "https" },
                { ":version", "HTTP/1.1" },
                { "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8" },
                { "accept-encoding", "gzip, deflate, sdch" },
                { "accept-language", "en-US,en;q=0.8" },
                { "cache-control", "max-age=0" },
                { "cookie", "noneofyourbusiness" },
                { "user-agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko)" }
            };
            examples.Add(HeaderExample.Eleven, header);

            header = new Dictionary<string, string>
            {
                { "cache-control", "no-cache, no-store, must-revalidate, pre-check=0, post-check=0" },
                { "content-encoding", "gzip" },
                { "content-security-policy", "default-src https:; connect-src https:;" },
                { "content-type", "text/html;charset=utf-8" },
                { "date", "Wed, 22 Apr 2015 00:40:28 GMT" },
                { "expires", "Tue, 31 Mar 1981 05:00:00 GMT" },
                { "last-modified", "Wed, 22 Apr 2015 00:40:28 GMT" },
                { "ms", "ms" },
                { "pragma", "no-cache" },
                { "server", "tsa_b" },
                { "set-cookie", "noneofyourbusiness" },
                { "status", "200 OK" },
                { "strict-transport-security", "max-age=631138519" },
                { "version", "HTTP/1.1" },
                { "x-connection-hash", "e176fe40accc1e2c613a34bc1941aa98" },
                { "x-content-type-options", "nosniff" },
                { "x-frame-options", "SAMEORIGIN" },
                { "x-response-time", "22" },
                { "x-transaction", "a54142ede693444d9" },
                { "x-twitter-response-tags", "BouncerCompliant" },
                { "x-ua-compatible", "IE=edge,chrome=1" },
                { "x-xss-protection", "1; mode=block" }
            };
            examples.Add(HeaderExample.Twentytwo, header);

            header = new Dictionary<string, string>
            {
                { "Cache-Control", "no-cache" },
                { "Content-Encoding", "gzip" },
                { "Content-Security-Policy", "default-src *; script-src assets-cdn.github.com ..." },
                { "Content-Type", "text/html; charset=utf-8" },
                { "Date", "Fri, 10 Apr 2015 02:15:38 GMT" },
                { "Server", "GitHub.com" },
                { "Set-Cookie", "_gh_sess=eyJzZXNza...; path=/; secure; HttpOnly" },
                { "Status", "200 OK" },
                { "Strict-Transport-Security", "max-age=31536000; includeSubdomains; preload" },
                { "Transfer-Encoding", "chunked" },
                { "Vary", "X-PJAX" },
                { "X-Content-Type-Options", "nosniff" },
                { "X-Frame-Options", "deny" },
                { "X-GitHub-Request-Id", "1" },
                { "X-GitHub-Session-Id", "1" },
                { "X-GitHub-User", "buchgr" },
                { "X-Request-Id", "28f245e02fc872dcf7f31149e52931dd" },
                { "X-Runtime", "0.082529" },
                { "X-Served-By", "b9c2a233f7f3119b174dbd8be2" },
                { "X-UA-Compatible", "IE=Edge,chrome=1" },
                { "X-XSS-Protection", "1; mode=block" },
                { "Via", "http/1.1 ir50.fp.bf1.yahoo.com (ApacheTrafficServer)" },
                { "Content-Language", "en" },
                { "Connection", "keep-alive" },
                { "Pragma", "no-cache" },
                { "Expires", "Sat, 01 Jan 2000 00:00:00 GMT" },
                { "X-Moose", "majestic" },
                { "x-ua-compatible", "IE=edge" },
                { "CF-Cache-Status", "HIT" },
                { "CF-RAY", "6a47f4f911e3-" }
            };
            examples.Add(HeaderExample.Thirty, header);

            return examples;
        }
    }
}
