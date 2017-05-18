// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Win32.SafeHandles;
    using Xunit.Abstractions;

    public sealed class CodeTimer : IDisposable
    {
        readonly ITestOutputHelper output;

        readonly long startTime;
        readonly ulong startCycles;
        readonly string testText;
        readonly int gen0Start;
        readonly int gen1Start;
        readonly int gen2Start;

        CodeTimer(bool startFresh, ITestOutputHelper output,
            string format, params object[] args)
        {
            this.output = output;
            if (startFresh)
            {
                PrepareForOperation();
            }

            this.testText = string.Format(format, args);

            this.gen0Start = GC.CollectionCount(0);
            this.gen1Start = GC.CollectionCount(1);
            this.gen2Start = GC.CollectionCount(2);

            // Get the time before returning so that any code above doesn't 
            // impact the time.
            this.startTime = Stopwatch.GetTimestamp();
#if NET452
            var handle = Process.GetCurrentProcess().Handle;
#else
            var handle = Process.GetCurrentProcess().SafeHandle.DangerousGetHandle();
#endif
            this.startCycles = CycleTime.Process(new SafeWaitHandle(handle, false));
        }

        /// <summary>
        ///     Times the operation in <paramref name="operation" />.
        /// </summary>
        /// <param name="text">
        ///     The text to display along with the timing information.
        /// </param>
        /// <param name="iterations">
        ///     The number of times to execute <paramref name="operation" />.
        /// </param>
        /// <param name="operation">
        ///     Action to execute.
        /// </param>
        public static void Time(string text, ITestOutputHelper output, int iterations, Action operation)
        {
            Time(false, output, text, iterations, operation);
        }

        /// <summary>
        ///     Times the operation in <paramref name="operation" />.
        /// </summary>
        /// <param name="startFresh">
        ///     If true, forces a GC in order to count just new garbage collections.
        /// </param>
        /// <param name="text">
        ///     The text to display along with the timing information.
        /// </param>
        /// <param name="iterations">
        ///     The number of times to execute <paramref name="operation" />.
        /// </param>
        /// <param name="operation">
        ///     Action to execute.
        /// </param>
        public static void Time(bool startFresh, ITestOutputHelper output, string text, int iterations, Action operation)
        {
            operation();

            using (new CodeTimer(startFresh, output, text))
            {
                while (iterations-- > 0)
                {
                    operation();
                }
            }
        }

        public static void Benchmark<T>(Dictionary<string, T> subjects, string labelFormat, int iterations, ITestOutputHelper output, Action<T> operation)
        {
            foreach (KeyValuePair<string, T> subject in subjects)
            {
                KeyValuePair<string, T> subjectDef = subject;
                Time(true, output, string.Format(labelFormat, subject.Key), iterations, () => operation(subjectDef.Value));
            }
        }

        public static void TimeAsync(bool startFresh, ITestOutputHelper output, string text, int iterations, Func<Task> operation)
        {
            operation().Wait();

            using (new CodeTimer(startFresh, output, text))
            {
                ExecuteAsync(iterations, operation).Wait();
            }
        }

        static async Task ExecuteAsync(int iterations, Func<Task> operation)
        {
            while (iterations-- > 0)
            {
                await operation();
            }
        }

        public void Dispose()
        {
#if NET452
            var handle = Process.GetCurrentProcess().Handle;
#else
            var handle = Process.GetCurrentProcess().SafeHandle.DangerousGetHandle();
#endif
            ulong elapsedCycles = CycleTime.Process(new SafeWaitHandle(handle, false)) - this.startCycles;

            long elapsedTime = Stopwatch.GetTimestamp() - this.startTime;

            long milliseconds = elapsedTime * 1000 / Stopwatch.Frequency;

            if (false == string.IsNullOrEmpty(this.testText))
            {
                this.output.WriteLine("{0}", this.testText);
                this.output.WriteLine("    {0,7:N0}ms {1,11:N0}Kc (G0={2,4}, G1={3,4}, G2={4,4})",
                    milliseconds,
                    elapsedCycles / 1000,
                    GC.CollectionCount(0) - this.gen0Start,
                    GC.CollectionCount(1) - this.gen1Start,
                    GC.CollectionCount(2) - this.gen2Start);
            }
        }

        static void PrepareForOperation()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}