// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
#pragma warning disable 420
namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Internal;
    using System.Threading;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Native;
    using Timer = Native.Timer;

    class LoopExecutor : AbstractScheduledEventExecutor
    {
        const int DefaultBreakoutTime = 100; //ms
        static readonly TimeSpan DefaultBreakoutInterval = TimeSpan.FromMilliseconds(DefaultBreakoutTime);

        protected static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<LoopExecutor>();

        const int NotStartedState = 1;
        const int StartedState = 2;
        const int ShuttingDownState = 3;
        const int ShutdownState = 4;
        const int TerminatedState = 5;

        readonly ThreadLocalPool<WriteRequest> writeRequestPool = new ThreadLocalPool<WriteRequest>(handle => new WriteRequest(handle));
        readonly long preciseBreakoutInterval;
        readonly IQueue<IRunnable> taskQueue;
        readonly XThread thread;
        readonly TaskScheduler scheduler;
        readonly ManualResetEventSlim loopRunStart;
        readonly TaskCompletionSource terminationCompletionSource;
        readonly Loop loop;
        readonly Async asyncHandle;
        readonly Timer timerHandle;

        volatile int executionState = NotStartedState;

        long lastExecutionTime;
        long gracefulShutdownStartTime;
        long gracefulShutdownQuietPeriod;
        long gracefulShutdownTimeout;

        // Flag to indicate whether async handle should be used to wake up 
        // the loop, only accessed when InEventLoop is true
        bool wakeUp = true;

        public LoopExecutor(string threadName)
            : this(null, threadName, DefaultBreakoutInterval)
        {
        }

        public LoopExecutor(IEventLoopGroup parent, string threadName)
            : this(parent, threadName, DefaultBreakoutInterval)
        {
        }

        public LoopExecutor(IEventLoopGroup parent, string threadName, TimeSpan breakoutInterval) : base(parent)
        {
            this.preciseBreakoutInterval = (long)breakoutInterval.TotalMilliseconds;
            this.terminationCompletionSource = new TaskCompletionSource();
            this.taskQueue = PlatformDependent.NewMpscQueue<IRunnable>();
            this.scheduler = new ExecutorTaskScheduler(this);

            this.loop = new Loop();
            this.asyncHandle = new Async(this.loop, OnCallback, this);
            this.timerHandle = new Timer(this.loop, OnCallback, this);
            string name = $"{this.GetType().Name}:{this.loop.Handle}";
            if (!string.IsNullOrEmpty(threadName))
            {
                name = $"{name}({threadName})";
            }
            this.thread = new XThread(Run) { Name = name };
            this.loopRunStart = new ManualResetEventSlim(false, 1);
        }

        internal ThreadLocalPool<WriteRequest> WriteRequestPool => this.writeRequestPool;

        protected void Start()
        {
            if (this.executionState > NotStartedState)
            {
                throw new InvalidOperationException($"Invalid state {this.executionState}");
            }
            this.thread.Start(this);
        }

        internal Loop UnsafeLoop => this.loop;

        internal int LoopThreadId => this.thread.Id;

        static void Run(object state)
        {
            var loopExecutor = (LoopExecutor)state;
            loopExecutor.SetCurrentExecutor(loopExecutor);

            Task.Factory.StartNew(
                executor => ((LoopExecutor)executor).StartLoop(), state,
                CancellationToken.None,
                TaskCreationOptions.AttachedToParent,
                loopExecutor.scheduler);
        }

        static void OnCallback(object state) => ((LoopExecutor)state).OnCallback();

        void OnCallback()
        {
            if (this.IsShuttingDown)
            {
                this.ShuttingDown();
            }
            else
            {
                this.RunAllTasks(this.preciseBreakoutInterval);
            }
        }

        /// <summary>
        /// Called before run the loop in the loop thread.
        /// </summary>
        protected virtual void Initialize()
        {
            // NOOP
        }

        /// <summary>
        /// Called before stop the loop in the loop thread.
        /// </summary>
        protected virtual void Release()
        {
            // NOOP
        }

        internal void WaitForLoopRun(TimeSpan timeout) => this.loopRunStart.Wait(timeout);

        void StartLoop()
        {
            IntPtr handle = this.loop.Handle;
            try
            {
                this.UpdateLastExecutionTime();
                this.Initialize();
                if (Interlocked.CompareExchange(ref this.executionState, StartedState, NotStartedState) != NotStartedState)
                {
                    throw new InvalidOperationException($"Invalid {nameof(LoopExecutor)} state {this.executionState}");
                }
                this.loopRunStart.Set();
                this.loop.Run(uv_run_mode.UV_RUN_DEFAULT);
            }
            catch (Exception ex)
            {
                this.loopRunStart.Set();
                Logger.Error("Loop {}:{} run default error.", this.thread.Name, handle, ex);
                this.terminationCompletionSource.TrySetException(ex);
            }
            finally
            {
                Logger.Info("Loop {}:{} thread finished.", this.thread.Name, handle);
                this.CleanupAndTerminate();
            }
        }

        void StopLoop()
        {
            try
            {
                // Drop out from the loop so that it can be safely disposed,
                // other active handles will be closed by loop.Close()
                this.timerHandle.Stop();
                this.loop.Stop();
            }
            catch (Exception ex)
            {
                Logger.Error("{}: shutting down loop error", ex);
            }
        }

        void ShuttingDown()
        {
            Debug.Assert(this.InEventLoop);

            this.CancelScheduledTasks();

            if (this.gracefulShutdownStartTime == 0)
            {
                this.gracefulShutdownStartTime = this.GetLoopTime();
            }

            bool runTask;
            do
            {
                runTask = this.RunAllTasks();

                // Terminate if the quiet period is 0.
                if (this.gracefulShutdownQuietPeriod == 0)
                {
                    this.StopLoop();
                    return;
                }
            }
            while (runTask);

            long nanoTime = this.GetLoopTime();

            // Shutdown timed out
            if (nanoTime - this.gracefulShutdownStartTime <= this.gracefulShutdownTimeout
                && nanoTime - this.lastExecutionTime <= this.gracefulShutdownQuietPeriod)
            {
                // Wait for quiet period passed
                this.timerHandle.Start(DefaultBreakoutTime, 0); // 100ms
            }
            else
            {
                // No tasks were added for last quiet period
                this.StopLoop();
            }
        }

        void CleanupAndTerminate()
        {
            try
            {
                this.Cleanup();
            }
            finally
            {
                Interlocked.Exchange(ref this.executionState, TerminatedState);
                if (!this.taskQueue.IsEmpty)
                {
                    Logger.Warn($"{nameof(LoopExecutor)} terminated with non-empty task queue ({this.taskQueue.Count})");
                }
                this.terminationCompletionSource.TryComplete();
            }
        }

        void Cleanup()
        {
            IntPtr handle = this.loop.Handle;

            try
            {
                this.Release();
            }
            catch (Exception ex)
            {
                Logger.Warn("{}:{} release error {}", this.thread.Name, handle, ex);
            }

            SafeDispose(this.timerHandle);
            SafeDispose(this.asyncHandle);
            SafeDispose(this.loop);
            Logger.Info("{}:{} disposed.", this.thread.Name, handle);
        }

        static void SafeDispose(IDisposable handle)
        {
            try
            {
                Logger.Info("Disposing {}", handle.GetType());
                handle.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warn("{} dispose error {}", handle.GetType(), ex);
            }
        }

        void UpdateLastExecutionTime() => this.lastExecutionTime = this.GetLoopTime();

        long GetLoopTime()
        {
            this.loop.UpdateTime();
            return this.loop.Now;
        }

        void RunAllTasks(long timeout)
        {
            this.FetchFromScheduledTaskQueue();
            IRunnable task = this.PollTask();
            if (task == null)
            {
                this.AfterRunningAllTasks();
                return;
            }

            long start = this.GetLoopTime();
            long runTasks = 0;
            long executionTime;
            this.wakeUp = false;
            for (; ; )
            {
                SafeExecute(task);

                runTasks++;

                // Check timeout every 64 tasks because nanoTime() is relatively expensive.
                // XXX: Hard-coded value - will make it configurable if it is really a problem.
                if ((runTasks & 0x3F) == 0)
                {
                    executionTime = this.GetLoopTime();
                    if ((executionTime - start) >= timeout)
                    {
                        break;
                    }
                }

                task = this.PollTask();
                if (task == null)
                {
                    executionTime = this.GetLoopTime();
                    break;
                }
            }
            this.wakeUp = true;

            this.AfterRunningAllTasks();
            this.lastExecutionTime = executionTime;
        }

        void AfterRunningAllTasks()
        {
            if (this.IsShuttingDown)
            {
                // Immediate shutdown
                this.WakeUp(true);
                return;
            }

            long nextTimeout = DefaultBreakoutTime;
            if (!this.taskQueue.IsEmpty)
            {
                this.timerHandle.Start(nextTimeout, 0);
            }
            else
            {
                IScheduledRunnable nextScheduledTask = this.ScheduledTaskQueue.Peek();
                if (nextScheduledTask != null)
                {
                    PreciseTimeSpan wakeUpTimeout = nextScheduledTask.Deadline - PreciseTimeSpan.FromStart;
                    if (wakeUpTimeout.Ticks > 0)
                    {
                        nextTimeout = (long)wakeUpTimeout.ToTimeSpan().TotalMilliseconds;
                    }
                    this.timerHandle.Start(nextTimeout, 0);
                }
            }
        }

        bool FetchFromScheduledTaskQueue()
        {
            PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;
            IScheduledRunnable scheduledTask = this.PollScheduledTask(nanoTime);
            while (scheduledTask != null)
            {
                if (!this.taskQueue.TryEnqueue(scheduledTask))
                {
                    // No space left in the task queue add it back to the scheduledTaskQueue so we pick it up again.
                    this.ScheduledTaskQueue.Enqueue(scheduledTask);
                    return false;
                }
                scheduledTask = this.PollScheduledTask(nanoTime);
            }
            return true;
        }

        IRunnable PollTask() => PollTaskFrom(this.taskQueue);

        bool RunAllTasks()
        {
            bool fetchedAll;
            bool ranAtLeastOne = false;
            do
            {
                fetchedAll = this.FetchFromScheduledTaskQueue();
                if (RunAllTasksFrom(this.taskQueue))
                {
                    ranAtLeastOne = true;
                }
            }
            while (!fetchedAll); // keep on processing until we fetched all scheduled tasks.
            if (ranAtLeastOne)
            {
                this.lastExecutionTime = this.GetLoopTime();
            }
            return ranAtLeastOne;
        }

        static bool RunAllTasksFrom(IQueue<IRunnable> taskQueue)
        {
            IRunnable task = PollTaskFrom(taskQueue);
            if (task == null)
            {
                return false;
            }
            for (; ; )
            {
                SafeExecute(task);
                task = PollTaskFrom(taskQueue);
                if (task == null)
                {
                    return true;
                }
            }
        }

        static IRunnable PollTaskFrom(IQueue<IRunnable> taskQueue) =>
            taskQueue.TryDequeue(out IRunnable task) ? task : null;

        public override Task TerminationCompletion => this.terminationCompletionSource.Task;

        public override bool IsShuttingDown => this.executionState >= ShuttingDownState;

        public override bool IsShutdown => this.executionState >= ShutdownState;

        public override bool IsTerminated => this.executionState == TerminatedState;

        public override bool IsInEventLoop(XThread t) => this.thread == t;

        void WakeUp(bool inEventLoop)
        {
            // If the executor is not in the event loop, wake up the loop by async handle immediately.
            //
            // If the executor is in the event loop and in the middle of RunAllTasks, no need to 
            // wake up the loop again because this is normally called by the current running task.
            if (!inEventLoop || this.wakeUp)
            {
                this.asyncHandle.Send();
            }
        }

        public override void Execute(IRunnable task)
        {
            Contract.Requires(task != null);

            bool inEventLoop = this.InEventLoop;
            if (inEventLoop)
            {
                this.AddTask(task);
            }
            else
            {
                this.AddTask(task);
                if (this.IsShutdown)
                {
                    Reject($"{nameof(LoopExecutor)} terminated");
                }
            }
            this.WakeUp(inEventLoop);
        }

        void AddTask(IRunnable task)
        {
            if (this.IsShutdown)
            {
                Reject($"{nameof(LoopExecutor)} already shutdown");
            }
            if (!this.taskQueue.TryEnqueue(task))
            {
                Reject($"{nameof(LoopExecutor)} queue task failed");
            }
        }

        static void Reject(string message) => throw new RejectedExecutionException(message);

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            Contract.Requires(quietPeriod >= TimeSpan.Zero);
            Contract.Requires(timeout >= quietPeriod);

            if (this.IsShuttingDown)
            {
                return this.TerminationCompletion;
            }

            // In case of Shutdown called before the loop run
            this.loopRunStart.Wait();

            bool inEventLoop = this.InEventLoop;
            bool wakeUpLoop;
            int oldState;
            for (; ; )
            {
                if (this.IsShuttingDown)
                {
                    return this.TerminationCompletion;
                }
                int newState;
                wakeUpLoop = true;
                oldState = this.executionState;
                if (inEventLoop)
                {
                    newState = ShuttingDownState;
                }
                else
                {
                    switch (oldState)
                    {
                        case NotStartedState:
                        case StartedState:
                            newState = ShuttingDownState;
                            break;
                        default:
                            newState = oldState;
                            wakeUpLoop = false;
                            break;
                    }
                }
                if (Interlocked.CompareExchange(ref this.executionState, newState, oldState) == oldState)
                {
                    break;
                }
            }

            this.gracefulShutdownQuietPeriod = (long)quietPeriod.TotalMilliseconds;
            this.gracefulShutdownTimeout = (long)timeout.TotalMilliseconds;

            if (oldState == NotStartedState)
            {
                // If the loop is not yet running (e.g. Initialize failed) close all 
                // handles directly because wake up callback will not be executed. 
                this.CleanupAndTerminate();
            }
            else
            {
                if (wakeUpLoop)
                {
                    this.WakeUp(inEventLoop);
                }
            }

            return this.TerminationCompletion;
        }

        protected override IEnumerable<IEventExecutor> GetItems() => new[] { this };
    }
}
