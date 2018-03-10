// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading.Tasks.Sources;

    public interface IPromise
    {
        bool TryComplete();
        
        bool TrySetException(Exception exception);

        bool TrySetCanceled();

        IValueTaskSource Future { get; }

        bool SetUncancellable();
    }
}