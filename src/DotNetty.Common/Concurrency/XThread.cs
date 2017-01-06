// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate void XParameterizedThreadStart(object obj);

    [DebuggerDisplay("ID={threadID}, Name={Name}, IsExplicit={isExplicit}")]
    public sealed class XThread
    {
        static int maxThreadID = 0;
        int threadID;
#pragma warning disable CS0414
        bool isExplicit; // For debugging only
#pragma warning restore CS0414
        Task task;
        EventWaitHandle completed = new EventWaitHandle(false, EventResetMode.AutoReset);
        EventWaitHandle readyToStart = new EventWaitHandle(false, EventResetMode.AutoReset);

        [ThreadStatic]
        static XThread tls_this_thread;

        int GetNewThreadId()
        {
            maxThreadID = Interlocked.Increment(ref maxThreadID);
            return maxThreadID;
        }

        XThread()
        {
            threadID = GetNewThreadId();
            this.isExplicit = false;
            this.IsAlive = false;
        }

        void CreateLongRunningTask(XParameterizedThreadStart threadStartFunc)
        {
            this.task = Task.Factory.StartNew(
                () =>
                {
                    // We start the task running, then unleash it by signaling the readyToStart event.
                    // This is needed to avoid thread reuse for tasks (see below)
                    readyToStart.WaitOne();
                    // This is the first time we're using this thread, therefore the TLS slot must be empty
                    if (tls_this_thread != null)
                    {
                        System.Diagnostics.Debug.WriteLine("warning: tls_this_thread already created; OS thread reused");
                        Debug.Assert(false);
                    }
                    tls_this_thread = this;
                    threadStartFunc(this.startupParameter);
                    this.completed.Set();
                },
                CancellationToken.None,
                // .NET always creates a brand new thread for LongRunning tasks
                // This is not documented but unlikely to ever change:
                // https://github.com/dotnet/corefx/issues/2576#issuecomment-126693306
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public XThread(Action action)
        {
            this.threadID = GetNewThreadId();
            this.isExplicit = true;
            this.IsAlive = false;
            CreateLongRunningTask((x) => action());
        }

        public XThread(XParameterizedThreadStart threadStartFunc)
        {
            this.threadID = GetNewThreadId();
            this.isExplicit = true;
            this.IsAlive = false;
            CreateLongRunningTask(threadStartFunc);
        }

        public void Start()
        {
            readyToStart.Set();
            this.IsAlive = true;
        }

        object startupParameter;
        public void Start(object parameter)
        {
            this.startupParameter = parameter;
            this.Start();
        }

        public static void Sleep(int millisecondsTimeout)
        {
            Task.Delay(millisecondsTimeout).Wait();
        }

        public string Name { get; set; }

        public bool IsAlive { get; private set; }

        public static XThread CurrentThread
        {
            get
            {
                if (tls_this_thread == null) tls_this_thread = new XThread();
                return tls_this_thread;
            }
        }

        public bool Join(TimeSpan timeout)
        {
            return this.completed.WaitOne(timeout);
        }

        public bool Join(int millisecondsTimeout)
        {
            return this.completed.WaitOne(millisecondsTimeout);
        }
    }
}