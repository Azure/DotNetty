// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Runtime.CompilerServices;

    public interface IChannelFuture : ICriticalNotifyCompletion
    {
        bool IsCompleted { get; }

        void GetResult();

        void OnCompleted(Action<object> continuation, object state);
    }
}