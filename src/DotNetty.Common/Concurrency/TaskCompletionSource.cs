// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
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

        public bool TryComplete()
        {
            return this.TrySetResult(0);
        }

        public void Complete()
        {
            this.SetResult(0);
        }

        public bool setUncancellable()
        {
            // todo: support cancellation token where used
            return true;
        }

        public override string ToString()
        {
            return "TaskCompletionSource[status: " + this.Task.Status.ToString() + "]";
        }

        static TaskCompletionSource CreateVoidTcs()
        {
            var tcs = new TaskCompletionSource();
            tcs.TryComplete();
            return tcs;
        }
    }
}