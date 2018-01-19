// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Net;

    static class TestUtil
    {
        // Unit test collection name, this is to avoid paralleled unit 
        // testing starting bind/listen on loopback at the same time 
        // on multiple threads (xunit default to the number of CPU cores) 
        // causing channel initial operations to timeout.
        internal const string LibuvTransport = "Libuv Transport Tests";

        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        internal static readonly IPEndPoint LoopbackAnyPort = new IPEndPoint(IPAddress.IPv6Loopback, 0);
    }
}
