// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Utilities
{
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    ///     A class that returns the cycle times for a process or threads.
    /// </summary>
    public sealed class CycleTime
    {
        /// <summary>
        ///     Retrieves the cycle time for the specified thread.
        /// </summary>
        public static ulong Thread(SafeWaitHandle threadHandle)
        {
            ulong cycleTime;
            if (!QueryThreadCycleTime(threadHandle, out cycleTime))
            {
                throw new Win32Exception();
            }
            return cycleTime;
        }

        /// <summary>
        ///     Retrieves the cycle time for the current thread.
        /// </summary>
        public static ulong Thread()
        {
            ulong cycleTime;
            if (!QueryThreadCycleTime((IntPtr)(-2), out cycleTime))
            {
                throw new Win32Exception();
            }
            return cycleTime;
        }

        /// <summary>
        ///     Retrieves the sum of the cycle time of all threads of the specified
        ///     process.
        /// </summary>
        public static ulong Process(SafeWaitHandle processHandle)
        {
            ulong cycleTime;
            if (!QueryProcessCycleTime(processHandle, out cycleTime))
            {
                throw new Win32Exception();
            }
            return cycleTime;
        }

        [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool
            QueryThreadCycleTime(IntPtr threadHandle, out ulong cycleTime);

        [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool
            QueryThreadCycleTime(SafeWaitHandle threadHandle,
                out ulong cycleTime);

        [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool
            QueryProcessCycleTime(SafeWaitHandle processHandle,
                out ulong cycleTime);
    }
}