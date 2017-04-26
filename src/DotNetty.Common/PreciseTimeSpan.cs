// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using System.Diagnostics;

    public struct PreciseTimeSpan : IComparable<PreciseTimeSpan>, IEquatable<PreciseTimeSpan>
    {
        static readonly long StartTime = Stopwatch.GetTimestamp();
        static readonly double PrecisionRatio = (double)Stopwatch.Frequency / TimeSpan.TicksPerSecond;
        static readonly double ReversePrecisionRatio = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        readonly long ticks;

        PreciseTimeSpan(long ticks)
            : this()
        {
            this.ticks = ticks;
        }

        public long Ticks => this.ticks;

        public static readonly PreciseTimeSpan Zero = new PreciseTimeSpan(0);

        public static readonly PreciseTimeSpan MinusOne = new PreciseTimeSpan(-1);

        public static PreciseTimeSpan FromTicks(long ticks) => new PreciseTimeSpan(ticks);

        public static PreciseTimeSpan FromStart => new PreciseTimeSpan(GetTimeChangeSinceStart());

        public static PreciseTimeSpan FromTimeSpan(TimeSpan timeSpan) => new PreciseTimeSpan(TicksToPreciseTicks(timeSpan.Ticks));

        public static PreciseTimeSpan Deadline(TimeSpan deadline) => new PreciseTimeSpan(GetTimeChangeSinceStart() + TicksToPreciseTicks(deadline.Ticks));

        public static PreciseTimeSpan Deadline(PreciseTimeSpan deadline) => new PreciseTimeSpan(GetTimeChangeSinceStart() + deadline.ticks);

        static long TicksToPreciseTicks(long ticks) => Stopwatch.IsHighResolution ? (long)(ticks * PrecisionRatio) : ticks;

        public TimeSpan ToTimeSpan() => TimeSpan.FromTicks((long)(this.ticks * ReversePrecisionRatio));

        static long GetTimeChangeSinceStart() => Stopwatch.GetTimestamp() - StartTime;

        public bool Equals(PreciseTimeSpan other) => this.ticks == other.ticks;

        public override bool Equals(object obj)
        {
            if (obj is PreciseTimeSpan)
            {
                return this.Equals((PreciseTimeSpan)obj);
            }

            return false;
        }

        public override int GetHashCode() => this.ticks.GetHashCode();

        public int CompareTo(PreciseTimeSpan other) => this.ticks.CompareTo(other.ticks);

        public static bool operator ==(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1.ticks == t2.ticks;

        public static bool operator !=(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1.ticks != t2.ticks;

        public static bool operator >(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1.ticks > t2.ticks;

        public static bool operator <(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1.ticks < t2.ticks;

        public static bool operator >=(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1.ticks >= t2.ticks;

        public static bool operator <=(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1.ticks <= t2.ticks;

        public static PreciseTimeSpan operator +(PreciseTimeSpan t, TimeSpan duration)
        {
            long ticks = t.ticks + TicksToPreciseTicks(duration.Ticks);
            return new PreciseTimeSpan(ticks);
        }

        public static PreciseTimeSpan operator -(PreciseTimeSpan t, TimeSpan duration)
        {
            long ticks = t.ticks - TicksToPreciseTicks(duration.Ticks);
            return new PreciseTimeSpan(ticks);
        }

        public static PreciseTimeSpan operator -(PreciseTimeSpan t1, PreciseTimeSpan t2)
        {
            long ticks = t1.ticks - t2.ticks;
            return new PreciseTimeSpan(ticks);
        }
    }
}