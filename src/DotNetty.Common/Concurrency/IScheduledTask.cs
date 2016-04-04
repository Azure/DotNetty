// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public interface IScheduledTask
    {
        bool Cancel();

        PreciseTimeSpan Deadline { get; }

        Task Completion { get; }

        TaskAwaiter GetAwaiter();
    }
}