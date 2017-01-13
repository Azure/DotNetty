// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public static class ThreadDeathWatcher
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(ThreadDeathWatcher));

        static readonly IQueue<Entry> PendingEntries = PlatformDependent.NewMpscQueue<Entry>();
        static readonly Watcher watcher = new Watcher();
        static int started;
        static volatile Thread watcherThread;

        static ThreadDeathWatcher()
        {
            string poolName = "threadDeathWatcher";
            string serviceThreadPrefix = SystemPropertyUtil.Get("io.netty.serviceThreadPrefix");
            if (!string.IsNullOrEmpty(serviceThreadPrefix))
            {
                poolName = serviceThreadPrefix + poolName;
            }
        }

        /// Schedules the specified {@code task} to run when the specified {@code thread} dies.
        /// 
        /// @param thread the {@link Thread} to watch
        /// @param task the {@link Runnable} to run when the {@code thread} dies
        /// 
        /// @throws IllegalArgumentException if the specified {@code thread} is not alive
        public static void Watch(Thread thread, Action task)
        {
            Contract.Requires(thread != null);
            Contract.Requires(task != null);
            Contract.Requires(thread.IsAlive);

            Schedule(thread, task, true);
        }

        /// Cancels the task scheduled via {@link #watch(Thread, Runnable)}.
        public static void Unwatch(Thread thread, Action task)
        {
            Contract.Requires(thread != null);
            Contract.Requires(task != null);

            Schedule(thread, task, false);
        }

        static void Schedule(Thread thread, Action task, bool isWatch)
        {
            PendingEntries.TryEnqueue(new Entry(thread, task, isWatch));

            if (Interlocked.CompareExchange(ref started, 1, 0) == 0)
            {
                var watcherThread = new Thread(s => ((IRunnable)s).Run());
                watcherThread.Start(watcher);
                ThreadDeathWatcher.watcherThread = watcherThread;
            }
        }

        /// Waits until the thread of this watcher has no threads to watch and terminates itself.
        /// Because a new watcher thread will be started again on {@link #watch(Thread, Runnable)},
        /// this operation is only useful when you want to ensure that the watcher thread is terminated
        /// <strong>after</strong>
        /// your application is shut down and there's no chance of calling
        /// {@link #watch(Thread, Runnable)} afterwards.
        /// 
        /// @return {@code true} if and only if the watcher thread has been terminated
        public static bool AwaitInactivity(TimeSpan timeout)
        {
            Thread watcherThread = ThreadDeathWatcher.watcherThread;
            if (watcherThread != null)
            {
                watcherThread.Join(timeout);
                return !watcherThread.IsAlive;
            }
            else
            {
                return true;
            }
        }

        sealed class Watcher : IRunnable
        {
            readonly List<Entry> watchees = new List<Entry>();

            public void Run()
            {
                for (;;)
                {
                    this.FetchWatchees();
                    this.NotifyWatchees();

                    // Try once again just in case notifyWatchees() triggered watch() or unwatch().
                    this.FetchWatchees();
                    this.NotifyWatchees();

                    Thread.Sleep(1000);

                    if (this.watchees.Count == 0 && PendingEntries.IsEmpty)
                    {
                        // Mark the current worker thread as stopped.
                        // The following CAS must always success and must be uncontended,
                        // because only one watcher thread should be running at the same time.
                        bool stopped = Interlocked.CompareExchange(ref started, 0, 1) == 1;
                        Contract.Assert(stopped);

                        // Check if there are pending entries added by watch() while we do CAS above.
                        if (PendingEntries.IsEmpty)
                        {
                            // A) watch() was not invoked and thus there's nothing to handle
                            //    -> safe to terminate because there's nothing left to do
                            // B) a new watcher thread started and handled them all
                            //    -> safe to terminate the new watcher thread will take care the rest
                            break;
                        }

                        // There are pending entries again, added by watch()
                        if (Interlocked.CompareExchange(ref started, 1, 0) != 0)
                        {
                            // watch() started a new watcher thread and set 'started' to true.
                            // -> terminate this thread so that the new watcher reads from pendingEntries exclusively.
                            break;
                        }

                        // watch() added an entry, but this worker was faster to set 'started' to true.
                        // i.e. a new watcher thread was not started
                        // -> keep this thread alive to handle the newly added entries.
                    }
                }
            }

            void FetchWatchees()
            {
                for (;;)
                {
                    Entry e;
                    if (!PendingEntries.TryDequeue(out e))
                    {
                        break;
                    }

                    if (e.IsWatch)
                    {
                        this.watchees.Add(e);
                    }
                    else
                    {
                        this.watchees.Remove(e);
                    }
                }
            }

            void NotifyWatchees()
            {
                List<Entry> watchees = this.watchees;
                for (int i = 0; i < watchees.Count;)
                {
                    Entry e = watchees[i];
                    if (!e.Thread.IsAlive)
                    {
                        watchees.RemoveAt(i);
                        try
                        {
                            e.Task();
                        }
                        catch (Exception t)
                        {
                            Logger.Warn("Thread death watcher task raised an exception:", t);
                        }
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        sealed class Entry
        {
            internal readonly Thread Thread;
            internal readonly Action Task;
            internal readonly bool IsWatch;

            public Entry(Thread thread, Action task, bool isWatch)
            {
                this.Thread = thread;
                this.Task = task;
                this.IsWatch = isWatch;
            }

            public override int GetHashCode() => this.Thread.GetHashCode() ^ this.Task.GetHashCode();

            public override bool Equals(object obj)
            {
                if (obj == this)
                {
                    return true;
                }

                if (!(obj is Entry))
                {
                    return false;
                }

                var that = (Entry)obj;
                return this.Thread == that.Thread && this.Task == that.Task;
            }
        }
    }
}