// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Internal;
    using System.Threading;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Native;

    using Timer = DotNetty.Transport.Libuv.Native.Timer;

    public class LoopExecutor : AbstractScheduledEventExecutor, ILoopExecutor
    {
        static readonly TimeSpan DefaultBreakoutInterval = TimeSpan.FromMilliseconds(100);
        static readonly string DefaultWorkerThreadName = $"Libuv {nameof(LoopExecutor)} {0}";

        protected static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<LoopExecutor>();

        const int NotStartedState = 1;
        const int StartedState = 2;
        const int ShuttingDownState = 3;
        const int ShutdownState = 4;
        const int TerminatedState = 5;

        readonly PreciseTimeSpan preciseBreakoutInterval;
        readonly PreciseTimeSpan preciseTimerInterval;

        readonly IQueue<IRunnable> taskQueue;
        readonly XThread thread;
        readonly TaskScheduler scheduler;
        readonly TaskCompletionSource terminationCompletionSource;
        readonly Loop loop;
        readonly Async asyncHandle;
        readonly Timer timerHandle;

        volatile int executionState = NotStartedState;

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
            this.preciseBreakoutInterval = PreciseTimeSpan.FromTimeSpan(breakoutInterval);
            this.preciseTimerInterval = PreciseTimeSpan.FromTimeSpan(TimeSpan.FromTicks(breakoutInterval.Ticks * 2));

            this.terminationCompletionSource = new TaskCompletionSource();
            this.taskQueue = PlatformDependent.NewMpscQueue<IRunnable>();
            this.scheduler = new ExecutorTaskScheduler(this);

            this.loop = new Loop();
            this.asyncHandle = new Async(this.loop, RunAllTasksCallback, this);
            this.timerHandle = new Timer(this.loop, RunAllTasksCallback, this);
            string name = string.Format(DefaultWorkerThreadName, this.loop.Handle);
            if (!string.IsNullOrEmpty(threadName))
            {
                name = $"{name} ({threadName})";
            }
            this.thread = new XThread(RunLoop)
            {
                Name = name
            };
        }

        protected internal void Start()
        {
            if (this.executionState > NotStartedState)
            {
                throw new InvalidOperationException($"Invalid state {this.executionState}");
            }

            this.thread.Start(this);
        }

        Loop ILoopExecutor.UnsafeLoop => this.loop;

        internal int LoopThreadId => this.thread.Id;

        static void RunLoop(object state)
        {
            var loop = (LoopExecutor)state;
            loop.SetCurrentExecutor(loop);

            Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        loop.Initialize();
                        loop.executionState = StartedState;
                        loop.loop.Run(uv_run_mode.UV_RUN_DEFAULT);
                        Logger.Info("{}: {} run finished.", loop.thread.Name, loop.loop.Handle);
                        loop.terminationCompletionSource.TryComplete();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("{}: execution loop failed", loop.thread.Name, ex);
                        loop.terminationCompletionSource.TrySetException(ex);
                    }

                    loop.executionState = TerminatedState;
                },
                CancellationToken.None,
                TaskCreationOptions.None,
                loop.scheduler);
        }

        static void RunAllTasksCallback(object state)
        {
            var eventLoop = (LoopExecutor)state;
            if (eventLoop.IsShuttingDown)
            {
                eventLoop.ShutdownLoop();
            }
            else
            {
                eventLoop.RunAllTasks();
            }
        }

        protected virtual void Initialize()
        {
        }

        void ShutdownLoop()
        {
            try
            {
                this.Shutdown();
                this.timerHandle.Stop();
                this.asyncHandle.RemoveReference();
                this.timerHandle.RemoveReference();
                this.loop.Close();
            }
            catch (Exception exception)
            {
                Logger.Error("{}: stop loop failed", exception);
            }
        }

        protected virtual void Shutdown() => this.CancelScheduledTasks();

        void RunAllTasks()
        {
            this.FetchFromScheduledTaskQueue();

            PreciseTimeSpan deadline = PreciseTimeSpan.Deadline(this.preciseBreakoutInterval);
            long runTasks = 0;
            PreciseTimeSpan executionTime = PreciseTimeSpan.Zero;
            while (true)
            {
                IRunnable task = this.PollTask();
                if (task == null || this.IsShuttingDown)
                {
                    break;
                }

                try
                {
                    task.Run();
                }
                catch (Exception ex)
                {
                    Logger.Warn("A task raised an exception.", ex);
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
            }

            if (this.IsShuttingDown)
            {
                this.ShutdownLoop();
            }
            else
            {
                if (executionTime != PreciseTimeSpan.Zero)
                {
                    // Start the timer once to check the task queue later
                    this.timerHandle.Start(this.preciseTimerInterval.Ticks, 1);
                }
            }
        }

        void FetchFromScheduledTaskQueue()
        {
            PreciseTimeSpan nanoTime = PreciseTimeSpan.FromStart;
            IScheduledRunnable scheduledTask = this.PollScheduledTask(nanoTime);
            while (scheduledTask != null)
            {
                if (!this.taskQueue.TryEnqueue(scheduledTask))
                {
                    // No space left in the task queue add it back to the scheduledTaskQueue so we pick it up again.
                    this.ScheduledTaskQueue.Enqueue(scheduledTask);
                    break;
                }
                scheduledTask = this.PollScheduledTask(nanoTime);
            }
        }

        IRunnable PollTask()
        {
            Contract.Assert(this.InEventLoop);

            if (!this.taskQueue.TryDequeue(out IRunnable task))
            {
                if (!this.taskQueue.TryDequeue(out task) && !this.IsShuttingDown) // revisit queue as producer might have put a task in meanwhile
                {
                    IScheduledRunnable nextScheduledTask = this.ScheduledTaskQueue.Peek();
                    if (nextScheduledTask != null)
                    {
                        PreciseTimeSpan wakeupTimeout = nextScheduledTask.Deadline - PreciseTimeSpan.FromStart;
                        if (wakeupTimeout.Ticks > 0)
                        {
                            this.taskQueue.TryDequeue(out task);
                        }
                    }
                    else
                    {
                        this.taskQueue.TryDequeue(out task);
                    }
                }
            }

            return task;
        }

        public override bool IsShuttingDown => this.executionState >= ShuttingDownState;

        public override Task TerminationCompletion => this.terminationCompletionSource.Task;

        public override bool IsShutdown => this.executionState >= ShutdownState;

        public override bool IsTerminated => this.executionState == TerminatedState;

        public override bool IsInEventLoop(XThread t) => this.thread == t;

        public override void Execute(IRunnable task)
        {
            this.taskQueue.TryEnqueue(task);
            this.asyncHandle.Send();
        }

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            Contract.Requires(quietPeriod >= TimeSpan.Zero);
            Contract.Requires(timeout >= quietPeriod);

            if (this.IsShuttingDown)
            {
                return this.TerminationCompletion;
            }

            bool inEventLoop = this.InEventLoop;
            bool wakeup;
            while (true)
            {
                if (this.IsShuttingDown)
                {
                    return this.TerminationCompletion;
                }
                int newState;
                wakeup = true;
                int oldState = this.executionState;
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
                            wakeup = false;
                            break;
                    }
                }
#pragma warning disable 420
                if (Interlocked.CompareExchange(ref this.executionState, newState, oldState) == oldState)
#pragma warning restore 420
                {
                    break;
                }
            }

            if (wakeup)
            {
                this.asyncHandle.Send();
            }

            return this.TerminationCompletion;
        }
    }
}
