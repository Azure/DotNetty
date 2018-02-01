// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading.Tasks;

    public sealed class TaskCompletionSource : TaskCompletionSource<int>
    {
        public static readonly TaskCompletionSource Void = CreateVoidTcs();

        public TaskCompletionSource(object state)
            : base(state)
        {
        }

        public TaskCompletionSource()
        {
        }

        public bool IsVoid { get; private set; }

        public bool TryComplete() => this.TrySetResult(0);

        public void Complete() => this.SetResult(0);

        // todo: support cancellation token where used
        public bool SetUncancellable() => true;

        public override string ToString() => "TaskCompletionSource[status: " + this.Task.Status.ToString() + "]";

        static TaskCompletionSource CreateVoidTcs()
        {
            var tcs = new TaskCompletionSource();
            tcs.TrySetException(new InvalidOperationException("No operations are allowed on void TaskCompletionSource"));
            tcs.IsVoid = true;
            return tcs;
        }

        public TaskCompletionSource Unvoid() => this.IsVoid ? new TaskCompletionSource() : this;
    }
}