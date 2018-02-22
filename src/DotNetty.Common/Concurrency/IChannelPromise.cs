// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    public interface IChannelPromise
    {
        bool TryComplete(Exception exception = null);

        IChannelFuture Future { get; }

        bool SetUncancellable();
    }
}