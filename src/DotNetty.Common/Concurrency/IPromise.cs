// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    public interface IPromise
    {
        bool TryComplete();
        
        bool TrySetException(Exception exception);

        bool TrySetCanceled();

        bool SetUncancellable();
    }
}