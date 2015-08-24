// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    public class SingleThreadEventExecutor : IEventExecutor
    {
#pragma warning disable 420 // referencing volatile fields is fine in Interlocked methods

        const int ST_NOT_STARTED = 1;
        const int ST_STARTED = 2;
        const int ST_SHUTTING_DOWN = 3;
        const int ST_SHUTDOWN = 4;
        const int ST_TERMINATED = 5;
        const string DefaultWorkerThreadName = "SingleThreadEventExecutor worker";

        static readonly Action<object> DelegatingAction = action => ((Action)action)();
        static readonly TimeSpan DefaultShutdownQuietPeriod = TimeSpan.FromSeconds(2);
        static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(15);

        readonly MpscLinkedQueue<IRunnable> taskQueue = new MpscLinkedQueue<IRunnable>();
        Thread thread;
        volatile int state = ST_NOT_STARTED;
        readonly PriorityQueue<ScheduledTaskQueueNode> scheduledTaskQueue = new PriorityQueue<ScheduledTaskQueueNode>();
        readonly TimeSpan breakoutInterval;
        readonly PreciseTimeSpan preciseBreakoutInterval;
        PreciseTimeSpan lastExecutionTime;
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);
        readonly TaskScheduler scheduler;
        readonly TaskCompletionSource terminationCompletionSource;
        PreciseTimeSpan gracefulShutdownStartTime;
        bool disposed;
        PreciseTimeSpan gracefulShutdownQuietPeriod;
        PreciseTimeSpan gracefulShutdownTimeout;

        public SingleThreadEventExecutor(string threadName, TimeSpan breakoutInterval)
        {
            this.terminationCompletionSource = new TaskCompletionSource();
            this.breakoutInterval = breakoutInterval;
            this.preciseBreakoutInterval = PreciseTimeSpan.FromTimeSpan(breakoutInterval);
            this.scheduler = new ExecutorTaskScheduler(this);
            this.thread = new Thread(this.Loop)
            {
                IsBackground = true
            };
            if (string.IsNullOrEmpty(threadName))
            {
                this.thread.Name = DefaultWorkerThreadName;
            }
            else
            {
                this.thread.Name = threadName;
            }
            this.thread.Start();
        }

        /// <summary>
        /// Task Scheduler that will post work to this executor's queue.
        /// </summary>
        public TaskScheduler Scheduler
        {
            get { return this.scheduler; }
        }

        void Loop()
        {
            Task.Factory.StartNew(
                () =>
                {
                    if (Interlocked.CompareExchange(ref this.state, ST_STARTED, ST_NOT_STARTED) == ST_NOT_STARTED)
                    {
                        while (!this.ConfirmShutdown()) // todo: replace with ConfirmShutdown check
                        {
                            this.RunAllTasks(this.preciseBreakoutInterval);
                        }

                        this.CleanupAndTerminate(true);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                this.scheduler);
        }

        public bool InEventLoop
        {
            get { return this.IsInEventLoop(Thread.CurrentThread); }
        }

        public bool IsShuttingDown
        {
            get { return this.state >= ST_SHUTTING_DOWN; }
        }

        public Task TerminationCompletion
        {
            get { return this.terminationCompletionSource.Task; }
        }

        public bool IsShutdown
        {
            get { return this.state >= ST_SHUTDOWN; }
        }

        public bool IsTerminated
        {
            get { return this.state == ST_TERMINATED; }
        }

        public bool IsInEventLoop(Thread t)
        {
            return this.thread == t;
        }

        public IEventExecutor Unwrap()
        {
            return this;
        }

        public void Execute(IRunnable task)
        {
            this.taskQueue.Enqueue(task);
            this.semaphore.Release();

            // todo: honor Running
        }

        public void Execute(Action<object> action, object state)
        {
            this.Execute(new TaskQueueNode(action, state));
        }

        public void Execute(Action<object, object> action, object state1, object state2)
        {
            this.Execute(new TaskQueueNode2(action, state1, state2));
        }

        public void Execute(Action action)
        {
            this.Execute(DelegatingAction, action);
        }

        public Task SubmitAsync(Func<object, Task> func, object state)
        {
            var tcs = new TaskCompletionSource(state);
            // todo: allocation?
            this.Execute(async _ =>
            {
                var asTcs = (TaskCompletionSource)_;
                try
                {
                    await func(asTcs.Task.AsyncState);
                    asTcs.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: support cancellation
                    asTcs.TrySetException(ex);
                }
            }, tcs);
            return tcs.Task;
        }

        public Task SubmitAsync(Func<Task> func)
        {
            var tcs = new TaskCompletionSource();
            // todo: allocation?
            this.Execute(async _ =>
            {
                var asTcs = (TaskCompletionSource)_;
                try
                {
                    await func();
                    asTcs.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: support cancellation
                    asTcs.TrySetException(ex);
                }
            }, tcs);
            return tcs.Task;
        }

        public Task SubmitAsync(Func<object, object, Task> func, object state1, object state2)
        {
            var tcs = new TaskCompletionSource(state1);
            // todo: allocation?
            this.Execute(async (s1, s2) =>
            {
                var asTcs = (TaskCompletionSource)s1;
                try
                {
                    await func(asTcs.Task.AsyncState, s2);
                    asTcs.TryComplete();
                }
                catch (Exception ex)
                {
                    // todo: support cancellation
                    asTcs.TrySetException(ex);
                }
            }, tcs, state2);
            return tcs.Task;
        }

        public Task ShutdownGracefullyAsync()
        {
            return this.ShutdownGracefullyAsync(DefaultShutdownQuietPeriod, DefaultShutdownTimeout);
        }

        public Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            Contract.Requires(quietPeriod >= TimeSpan.Zero);
            Contract.Requires(timeout >= quietPeriod);

            if (this.IsShuttingDown)
            {
                return this.TerminationCompletion;
            }

            bool inEventLoop = this.InEventLoop;
            bool wakeup;
            int oldState;
            while (true)
            {
                if (this.IsShuttingDown)
                {
                    return this.TerminationCompletion;
                }
                int newState;
                wakeup = true;
                oldState = this.state;
                if (inEventLoop)
                {
                    newState = ST_SHUTTING_DOWN;
                }
                else
                {
                    switch (oldState)
                    {
                        case ST_NOT_STARTED:
                        case ST_STARTED:
                            newState = ST_SHUTTING_DOWN;
                            break;
                        default:
                            newState = oldState;
                            wakeup = false;
                            break;
                    }
                }
                if (Interlocked.CompareExchange(ref this.state, newState, oldState) == oldState)
                {
                    break;
                }
            }
            this.gracefulShutdownQuietPeriod = PreciseTimeSpan.FromTimeSpan(quietPeriod);
            this.gracefulShutdownTimeout = PreciseTimeSpan.FromTimeSpan(timeout);

            // todo: revisit
            //if (oldState == ST_NOT_STARTED)
            //{
            //    scheduleExecution();
            //}

            //if (wakeup)
            //{
            //    wakeup(inEventLoop);
            //}

            return this.TerminationCompletion;
        }

        protected bool ConfirmShutdown()
        {
            if (!this.IsShuttingDown)
            {
                return false;
            }

            if (!this.InEventLoop)
            {
                throw new InvalidOperationException("must be invoked from an event loop");
            }

            // todo: port
            //this.CancelScheduledTasks();

            if (this.gracefulShutdownStartTime == PreciseTimeSpan.Zero)
            {
                this.gracefulShutdownStartTime = PreciseTimeSpan.FromStart;
            }

            if (this.RunAllTasks()) // || runShutdownHooks())
            {
                if (this.IsShutdown)
                {
                    // Executor shut down - no new tasks anymore.
                    return true;
                }

                // There were tasks in the queue. Wait a little bit more until no tasks are queued for the quiet period.
                // todo: ???
                //wakeup(true);
                return false;
            }

            PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;

            if (this.IsShutdown || nanoTime - this.gracefulShutdownStartTime > this.gracefulShutdownTimeout)
            {
                return true;
            }

            if (nanoTime - this.lastExecutionTime <= this.gracefulShutdownQuietPeriod)
            {
                // Check if any tasks were added to the queue every 100ms.
                // TODO: Change the behavior of takeTask() so that it returns on timeout.
                // todo: ???
                //wakeup(true);
                Thread.Sleep(100);

                return false;
            }

            // No tasks were added for last quiet period - hopefully safe to shut down.
            // (Hopefully because we really cannot make a guarantee that there will be no execute() calls by a user.)
            return true;
        }

        protected void CleanupAndTerminate(bool success)
        {
            while (true)
            {
                int oldState = this.state;
                ;
                if (oldState >= ST_SHUTTING_DOWN || Interlocked.CompareExchange(ref this.state, ST_SHUTTING_DOWN, oldState) == oldState)
                {
                    break;
                }
            }

            // Check if confirmShutdown() was called at the end of the loop.
            if (success && this.gracefulShutdownStartTime == PreciseTimeSpan.Zero)
            {
                ExecutorEventSource.Log.Error(
                    string.Format("Buggy {0} implementation; {1}.ConfirmShutdown() must be called " + "before run() implementation terminates.",
                        typeof(IEventExecutor).Name,
                        typeof(SingleThreadEventExecutor).Name),
                    (string)null);
            }

            try
            {
                // Run all remaining tasks and shutdown hooks.
                while (true)
                {
                    if (this.ConfirmShutdown())
                    {
                        break;
                    }
                }
            }
            finally
            {
                try
                {
                    this.Cleanup();
                }
                finally
                {
                    Interlocked.Exchange(ref this.state, ST_TERMINATED);
                    if (!this.taskQueue.IsEmpty)
                    {
                        ExecutorEventSource.Log.Warning(string.Format("An event executor terminated with non-empty task queue ({0})", this.taskQueue.Count));
                    }

                    //firstRun = true;
                    this.terminationCompletionSource.Complete();
                }
            }
        }

        protected virtual void Cleanup()
        {
            // NOOP
        }

        public void Schedule(Action<object> action, object state, TimeSpan delay)
        {
            this.Schedule(action, state, delay, CancellationToken.None);
        }

        public void Schedule(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken)
        {
            var queueNode = new ScheduledTaskQueueNode(action, state, PreciseTimeSpan.Deadline(delay), cancellationToken);
            if (this.InEventLoop)
            {
                this.scheduledTaskQueue.Enqueue(queueNode);
            }
            else
            {
                this.Execute(e => ((SingleThreadEventExecutor)e).scheduledTaskQueue.Enqueue(queueNode), this); // it is an allocation but it should not happen often (cross-thread scheduling)
            }
        }

        public void Schedule(Action action, TimeSpan delay)
        {
            this.Schedule(action, delay, CancellationToken.None);
        }

        public void Schedule(Action action, TimeSpan delay, CancellationToken cancellationToken)
        {
            // todo: check for allocation
            this.Schedule(_ => action(), null, delay, cancellationToken);
        }

        protected bool RunAllTasks()
        {
            this.FetchFromScheduledTaskQueue();
            IRunnable task = this.PollTask();
            if (task == null)
            {
                return false;
            }

            while (true)
            {
                try
                {
                    task.Run();
                }
                catch (Exception ex)
                {
                    ExecutorEventSource.Log.Warning("A task raised an exception.", ex);
                }

                task = this.PollTask();
                if (task == null)
                {
                    this.lastExecutionTime = PreciseTimeSpan.FromStart;
                    return true;
                }
            }
        }

        bool RunAllTasks(PreciseTimeSpan timeout)
        {
            this.FetchFromScheduledTaskQueue();
            IRunnable task = this.PollTask();
            if (task == null)
            {
                return false;
            }

            PreciseTimeSpan deadline = PreciseTimeSpan.Deadline(timeout);
            long runTasks = 0;
            PreciseTimeSpan executionTime;
            while (true)
            {
                try
                {
                    task.Run();
                }
                catch (Exception ex)
                {
                    ExecutorEventSource.Log.Warning("A task raised an exception.", ex);
                }

                runTasks++;

                // Check timeout every 64 tasks because nanoTime() is relatively expensive.
                // XXX: Hard-coded value - will make it configurable if it is really a problem.
                if ((runTasks & 0x3F) == 0)
                {
                    executionTime = PreciseTimeSpan.FromStart;
                    if (executionTime >= deadline)
                    {
                        break;
                    }
                }

                task = this.PollTask();
                if (task == null)
                {
                    executionTime = PreciseTimeSpan.FromStart;
                    break;
                }
            }

            this.lastExecutionTime = executionTime;
            return true;
        }

        void FetchFromScheduledTaskQueue()
        {
            if (this.HasScheduledTasks())
            {
                PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;
                while (true)
                {
                    ScheduledTaskQueueNode scheduledTask = this.PollScheduledTask(nanoTime);
                    if (scheduledTask == null)
                    {
                        break;
                    }
                    this.taskQueue.Enqueue(scheduledTask);
                    this.semaphore.Release();
                }
            }
        }

        bool HasScheduledTasks()
        {
            ScheduledTaskQueueNode scheduledTask = this.scheduledTaskQueue.Peek();
            return scheduledTask != null && scheduledTask.Deadline <= PreciseTimeSpan.FromStart;
        }

        ScheduledTaskQueueNode PollScheduledTask(PreciseTimeSpan nanoTime)
        {
            Debug.Assert(this.InEventLoop);

            ScheduledTaskQueueNode scheduledTask = this.scheduledTaskQueue.Peek();
            if (scheduledTask == null)
            {
                return null;
            }

            if (scheduledTask.Deadline <= nanoTime)
            {
                this.scheduledTaskQueue.Dequeue();
                return scheduledTask;
            }
            return null;
        }

        IRunnable PollTask()
        {
            Debug.Assert(this.InEventLoop);
            this.semaphore.Wait(this.breakoutInterval);
            return this.taskQueue.Dequeue();
        }

        //public void Shutdown(TimeSpan gracePeriod)
        //{
        //    this.Running = false;
        //    this.Executor.Shutdown(gracePeriod);
        //}

        //public Task GracefulShutdown(TimeSpan gracePeriod)
        //{
        //    this.Shutdown(gracePeriod);
        //    return TaskRunner.Delay(gracePeriod);
        //}

        //public void Stop()
        //{
        //    this.Executor.Shutdown();
        //}

        #region IDisposable members

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool isDisposing)
        {
            if (!this.disposed)
            {
                if (isDisposing)
                {
                    //this.Shutdown(TimeSpan.Zero);
                    this.thread = null;
                }
            }

            this.disposed = true;
        }

        #endregion

        sealed class TaskQueueNode : MpscLinkedQueueNode<IRunnable>, IRunnable
        {
            readonly Action<object> action;
            readonly object state;

            public TaskQueueNode(Action<object> action, object state)
            {
                this.action = action;
                this.state = state;
            }

            public override IRunnable Value
            {
                get { return this; }
            }

            public void Run()
            {
                this.action(this.state);
            }
        }

        sealed class TaskQueueNode2 : MpscLinkedQueueNode<IRunnable>, IRunnable
        {
            readonly Action<object, object> action;
            readonly object state1;
            readonly object state2;

            public TaskQueueNode2(Action<object, object> action, object state1, object state2)
            {
                this.action = action;
                this.state1 = state1;
                this.state2 = state2;
            }

            public override IRunnable Value
            {
                get { return this; }
            }

            public void Run()
            {
                this.action(this.state1, this.state2);
            }
        }

        class ScheduledTaskQueueNode : MpscLinkedQueueNode<IRunnable>, IRunnable, IComparable<ScheduledTaskQueueNode>
        {
            readonly Action<object> action;
            readonly object state;

            public ScheduledTaskQueueNode(Action<object> action, object state, PreciseTimeSpan deadline,
                CancellationToken cancellationToken)
            {
                this.action = action;
                this.state = state;
                this.Deadline = deadline;
                this.CancellationToken = cancellationToken;
            }

            public PreciseTimeSpan Deadline { get; private set; }

            public CancellationToken CancellationToken { get; private set; }

            public int CompareTo(ScheduledTaskQueueNode other)
            {
                Contract.Requires(other != null);

                return this.Deadline.CompareTo(other.Deadline);
            }

            public override IRunnable Value
            {
                get { return this; }
            }

            public void Run()
            {
                if (!this.CancellationToken.IsCancellationRequested)
                {
                    this.action(this.state);
                }
            }
        }

        class ScheduledTaskQueueNode2 : MpscLinkedQueueNode<IRunnable>, IRunnable, IComparable<ScheduledTaskQueueNode>
        {
            readonly Action<object, object> action;
            readonly object state1;
            readonly object state2;

            public ScheduledTaskQueueNode2(Action<object, object> action, object state1, object state2, PreciseTimeSpan deadline,
                CancellationToken cancellationToken)
            {
                this.action = action;
                this.state1 = state1;
                this.state2 = state2;
                this.Deadline = deadline;
                this.CancellationToken = cancellationToken;
            }

            public PreciseTimeSpan Deadline { get; private set; }

            public CancellationToken CancellationToken { get; private set; }

            public int CompareTo(ScheduledTaskQueueNode other)
            {
                Contract.Requires(other != null);

                return this.Deadline.CompareTo(other.Deadline);
            }

            public override IRunnable Value
            {
                get { return this; }
            }

            public void Run()
            {
                if (!this.CancellationToken.IsCancellationRequested)
                {
                    this.action(this.state1, this.state2);
                }
            }
        }
    }
}