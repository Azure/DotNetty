// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IPromise
    {
        Task Task { get; }

        bool IsVoid { get; }

        bool TryComplete();

        void Complete();

        bool TrySetException(Exception ex);

        bool TrySetException(IEnumerable<Exception> ex);

        void SetException(Exception ex);

        bool TrySetCanceled();

        void SetCanceled();

        bool SetUncancellable();
    }
}