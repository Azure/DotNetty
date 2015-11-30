// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Logging
{
    using DotNetty.Common.Internal.Logging;

    public enum LogLevel
    {
        TRACE = InternalLogLevel.TRACE,
        DEBUG = InternalLogLevel.DEBUG,
        INFO = InternalLogLevel.INFO,
        WARN = InternalLogLevel.WARN,
        ERROR = InternalLogLevel.ERROR,
    }
}