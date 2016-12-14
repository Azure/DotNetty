// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Platform
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate void XParameterizedThreadStart(object obj);

    [DebuggerDisplay("ID={threadID}, Name={Name}, IsExplicit={isExplicit}")]
    public class XThread
    {
        private static int maxThreadID = 0;
        private int threadID;
#pragma warning disable CS0414
        private bool isExplicit; // For debugging only
#pragma warning restore CS0414
        private Task task;
        private EventWaitHandle completed = new EventWaitHandle(false, EventResetMode.AutoReset);
        private EventWaitHandle readyToStart = new EventWaitHandle(false, EventResetMode.AutoReset);

        private static ThreadLocal<XThread> tlocal = new ThreadLocal<XThread>(() => new XThread());

        private int GetNewThreadId()
        {
            maxThreadID = Interlocked.Increment(ref maxThreadID);
            return maxThreadID;
        }

        private XThread()
        {
            threadID = GetNewThreadId();
            this.isExplicit = false;
            this.IsAlive = false;
        }

        private void CreateLongRunningTask(XParameterizedThreadStart threadStartFunc)
        {
            this.task = Task.Factory.StartNew(
                () =>
                {
                    // We start the task running, then unleash it by signaling the readyToStart event.
                    // This is needed to avoid thread reuse for tasks (see below)
                    readyToStart.WaitOne();
                    // This is the first time we're using this thread, therefore it cannot have XThread created
                    //Debug.Assert(!tlocal.IsValueCreated);
                    if (tlocal.IsValueCreated)
                    {
                        System.Diagnostics.Debug.WriteLine("warning: tlocal already created; OS thread reused");
                        Debug.Assert(false);
                    }
                    tlocal.Value = this;
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
            readyToStart.Set();
            this.IsAlive = true;
        }

        public static void Sleep(int millisecondsTimeout)
        {
            Task.Delay(millisecondsTimeout).Wait();
        }

        public string Name;

        // NYI
        public bool IsBackground;

        public bool IsAlive { get; private set; }

        public static XThread CurrentThread
        {
            get
            {
                return tlocal.Value;
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