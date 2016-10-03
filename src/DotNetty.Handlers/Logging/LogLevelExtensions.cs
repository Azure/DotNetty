// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Logging
{
    using DotNetty.Common.Internal.Logging;

    public static class LogLevelExtensions
    {
        public static InternalLogLevel ToInternalLevel(this LogLevel level) => (InternalLogLevel)level;
    }
}