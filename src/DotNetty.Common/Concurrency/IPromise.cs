// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPromise
    {
        ValueTask ValueTask { get; }
        
        bool TryComplete();
        
        bool TrySetException(Exception exception);

        bool TrySetCanceled(CancellationToken cancellationToken = default(CancellationToken));

        bool SetUncancellable();
    }
}