// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    /// <summary>
    /// Time utility class.
    /// </summary>
    public static class TimeUtil
    {
        static TimeUtil()
        {
        }

        /// <summary>
        /// Compare two timespan objects
        /// </summary>
        /// <param name="t1">first timespan object</param>
        /// <param name="t2">two timespan object</param>
        public static TimeSpan Max(TimeSpan t1, TimeSpan t2)
        {
            return t1 > t2 ? t1 : t2;
        }

        /// <summary>
        /// Gets the system time.
        /// </summary>
        /// <returns>The system time.</returns>
        public static TimeSpan GetSystemTime()
        {
            return TimeSpan.FromMilliseconds(Environment.TickCount);
        }
    
    }
}

