// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate void XParameterizedThreadStart(object obj);

    [DebuggerDisplay("ID={threadId}, Name={Name}, IsExplicit={isExplicit}")]
    public sealed class XThread
    {
        static int maxThreadId;

        [ThreadStatic]
        static XThread currentThread;

        readonly int threadId;
#pragma warning disable CS0414
        readonly bool isExplicit; // For debugging only
#pragma warning restore CS0414
        Task task;
        readonly EventWaitHandle completed = new EventWaitHandle(false, EventResetMode.AutoReset);
        readonly EventWaitHandle readyToStart = new EventWaitHandle(false, EventResetMode.AutoReset);
        object startupParameter;

        static int GetNewThreadId() => Interlocked.Increment(ref maxThreadId);

        XThread()
        {
            this.threadId = GetNewThreadId();
            this.isExplicit = false;
            this.IsAlive = false;
        }

        public XThread(Action action)
        {
            this.threadId = GetNewThreadId();
            this.isExplicit = true;
            this.IsAlive = false;
            this.CreateLongRunningTask(x => action());
        }

        public XThread(XParameterizedThreadStart threadStartFunc)
        {
            this.threadId = GetNewThreadId();
            this.isExplicit = true;
            this.IsAlive = false;
            this.CreateLongRunningTask(threadStartFunc);
        }

        public void Start()
        {
            this.readyToStart.Set();
            this.IsAlive = true;
        }

        void CreateLongRunningTask(XParameterizedThreadStart threadStartFunc)
        {
            this.task = Task.Factory.StartNew(
                () =>
                {
                    // We start the task running, then unleash it by signaling the readyToStart event.
                    // This is needed to avoid thread reuse for tasks (see below)
                    this.readyToStart.WaitOne();
                    // This is the first time we're using this thread, therefore the TLS slot must be empty
                    if (currentThread != null)
                    {
                        Debug.WriteLine("warning: currentThread already created; OS thread reused");
                        Debug.Assert(false);
                    }
                    currentThread = this;
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

        public void Start(object parameter)
        {
            this.startupParameter = parameter;
            this.Start();
        }

        public static void Sleep(int millisecondsTimeout) => Task.Delay(millisecondsTimeout).Wait();

        public int Id => this.threadId;

        public string Name { get; set; }

        public bool IsAlive { get; private set; }

        public static XThread CurrentThread => currentThread ?? (currentThread = new XThread());

        public bool Join(TimeSpan timeout) => this.completed.WaitOne(timeout);

        public bool Join(int millisecondsTimeout) => this.completed.WaitOne(millisecondsTimeout);
    }
}