// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public sealed class ExecutorTaskScheduler : TaskScheduler
    {
        readonly IEventExecutor executor;
        bool started;
        readonly Action<object> executorCallback;

        public ExecutorTaskScheduler(IEventExecutor executor)
        {
            this.executor = executor;
            this.executorCallback = this.ExecutorCallback;
        }

        protected override void QueueTask(Task task)
        {
            if (this.started)
            {
                this.executor.Execute(this.executorCallback, task);
            }
            else
            {
                // hack: 
                this.started = true;
                this.TryExecuteTask(task);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            //return false;
            if (!this.executor.InEventLoop)
            {
                return false;
            }

            // If the task was previously queued, remove it from the queue 
            if (taskWasPreviouslyQueued)
            {
                // Try to run the task.  
                if (this.TryDequeue(task))
                {
                    return this.TryExecuteTask(task);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return this.TryExecuteTask(task);
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            throw new NotSupportedException();
        }

        protected override bool TryDequeue(Task task)
        {
            return this.executor.InEventLoop;
        }

        void ExecutorCallback(object state)
        {
            var task = (Task)state;

            if (task.IsCompleted)
            {
                return;
            }

            this.TryExecuteTask(task);
        }
    }
}