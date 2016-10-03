// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Performance.Utilities
{
    using DotNetty.Common.Concurrency;

    public class TaskCompletionSourceFinishedSignal : IReadFinishedSignal
    {
        readonly TaskCompletionSource tcs;

        public TaskCompletionSourceFinishedSignal(TaskCompletionSource tcs)
        {
            this.tcs = tcs;
        }

        public void Signal() => this.tcs.TryComplete();

        public bool Finished => this.tcs.Task.IsCompleted;
    }
}