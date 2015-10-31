// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    class EventSourceLoggerFactory : InternalLoggerFactory
    {
        protected internal override IInternalLogger NewInstance(string name)
        {
            return new EventSourceLogger(name);
        }
    }
}