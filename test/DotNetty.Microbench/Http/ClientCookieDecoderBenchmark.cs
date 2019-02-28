// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Http
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;
    using DotNetty.Codecs.Http.Cookies;
    using DotNetty.Common;

    [CoreJob]
    [BenchmarkCategory("Http")]
    public class ClientCookieDecoderBenchmark
    {
        const string CookieString = 
            "__Host-user_session_same_site=fgfMsM59vJTpZg88nxqKkIhgOt0ADF8LX8wjMMbtcb4IJMufWCnCcXORhbo9QMuyiybdtx; " 
            + "path=/; expires=Mon, 28 Nov 2016 13:56:01 GMT; secure; HttpOnly";

        [GlobalSetup]
        public void GlobalSetup()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
        }

        [Benchmark]
        public ICookie DecodeCookieWithRfc1123ExpiresField() => ClientCookieDecoder.StrictDecoder.Decode(CookieString);
    }
}
