// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using DotNetty.Transport.Libuv.Native;

    interface ILoopExecutor
    {
        Loop UnsafeLoop { get; }
    }
}
