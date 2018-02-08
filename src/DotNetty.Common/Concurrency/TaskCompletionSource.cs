// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System.Threading.Tasks;

    public sealed class TaskCompletionSource : TaskCompletionSource<int>, IPromise
    {
        public TaskCompletionSource(object state)
            : base(state)
        {
        }

        public TaskCompletionSource()
        {
            
        }

        Task IPromise.Task => this.Task;

        public bool IsVoid => false; 

        public bool TryComplete() => this.TrySetResult(0);

        public void Complete() => this.SetResult(0);

        // todo: support cancellation token where used
        public bool SetUncancellable() => true;

        public override string ToString() => "TaskCompletionSource[status: " + this.Task.Status.ToString() + "]";
    }
}