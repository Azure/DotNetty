// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using Nito;

    public sealed class ResourceLeakDetector
    {
        const string PropLevel = "io.netty.leakDetection.level";
        const DetectionLevel DefaultLevel = DetectionLevel.Simple;

        const string PropMaxRecords = "io.netty.leakDetection.maxRecords";
        const int DefaultMaxRecords = 4;
        static readonly int MaxRecords;

        readonly ConditionalWeakTable<object, GCNotice> gcNotificationMap = new ConditionalWeakTable<object, GCNotice>();

        /**
         * Represents the level of resource leak detection.
         */

        public enum DetectionLevel
        {
            /// <summary>
            ///     Disables resource leak detection.
            /// </summary>
            Disabled,

            /// <summary>
            ///     Enables simplistic sampling resource leak detection which reports there is a leak or not,
            ///     at the cost of small overhead (default).
            /// </summary>
            Simple,

            /// <summary>
            ///     Enables advanced sampling resource leak detection which reports where the leaked object was accessed
            ///     recently at the cost of high overhead.
            /// </summary>
            Advanced,

            /// <summary>
            ///     Enables paranoid resource leak detection which reports where the leaked object was accessed recently,
            ///     at the cost of the highest possible overhead (for testing purposes only).
            /// </summary>
            Paranoid
        }

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ResourceLeakDetector>();

        static ResourceLeakDetector()
        {
            // If new property name is present, use it
            string levelStr = SystemPropertyUtil.Get(PropLevel, DefaultLevel.ToString());
            DetectionLevel level;
            if (!Enum.TryParse(levelStr, true, out level))
            {
                level = DefaultLevel;
            }

            MaxRecords = SystemPropertyUtil.GetInt(PropMaxRecords, DefaultMaxRecords);

            Level = level;
            if (Logger.DebugEnabled)
            {
                Logger.Debug("{}: {}", PropLevel, level.ToString().ToLowerInvariant());
                Logger.Debug("{}: {}", PropMaxRecords, MaxRecords);
            }
        }

        static readonly int DEFAULT_SAMPLING_INTERVAL = 113;

        /// <summary>
        ///     Gets or sets resource leak detection level
        /// </summary>
        public static DetectionLevel Level { get; set; }

        /// Returns <c>true</c> if resource leak detection is enabled.
        public static bool Enabled => Level > DetectionLevel.Disabled;

        readonly ConcurrentDictionary<string, bool> reportedLeaks = new ConcurrentDictionary<string, bool>();

        readonly string resourceType;
        readonly int samplingInterval;
        readonly long maxActive;
        long active;
        int loggedTooManyActive;

        long leakCheckCnt;

        public ResourceLeakDetector(string resourceType)
            : this(resourceType, DEFAULT_SAMPLING_INTERVAL, long.MaxValue)
        {
        }

        public ResourceLeakDetector(string resourceType, int samplingInterval, long maxActive)
        {
            Contract.Requires(resourceType != null);
            Contract.Requires(samplingInterval > 0);
            Contract.Requires(maxActive > 0);

            this.resourceType = resourceType;
            this.samplingInterval = samplingInterval;
            this.maxActive = maxActive;
        }

        public static ResourceLeakDetector Create<T>() => new ResourceLeakDetector(StringUtil.SimpleClassName<T>());

        public static ResourceLeakDetector Create<T>(int samplingInterval, long maxActive) => new ResourceLeakDetector(StringUtil.SimpleClassName<T>(), samplingInterval, maxActive);

        /// <summary>
        ///     Creates a new <see cref="IResourceLeak" /> which is expected to be closed via <see cref="IResourceLeak.Close()" />
        ///     when the
        ///     related resource is deallocated.
        /// </summary>
        /// <returns>the <see cref="IResourceLeak" /> or <c>null</c></returns>
        public IResourceLeak Open(object obj)
        {
            DetectionLevel level = Level;
            switch (level)
            {
                case DetectionLevel.Disabled:
                    return null;
                case DetectionLevel.Paranoid:
                    this.CheckForCountLeak(level);
                    return new DefaultResourceLeak(this, obj);
                case DetectionLevel.Simple:
                case DetectionLevel.Advanced:
                    if (this.leakCheckCnt++ % this.samplingInterval == 0)
                    {
                        this.CheckForCountLeak(level);
                        return new DefaultResourceLeak(this, obj);
                    }
                    else
                    {
                        return null;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal void CheckForCountLeak(DetectionLevel level)
        {
            // Report too many instances.
            int interval = level == DetectionLevel.Paranoid ? 1 : this.samplingInterval;
            if (Volatile.Read(ref this.active) * interval > this.maxActive
                && Interlocked.CompareExchange(ref this.loggedTooManyActive, 0, 1) == 0)
            {
                Logger.Error("LEAK: You are creating too many " + this.resourceType + " instances.  " + this.resourceType + " is a shared resource that must be reused across the AppDomain," +
                    "so that only a few instances are created.");
            }
        }

        internal void Report(IResourceLeak resourceLeak)
        {
            string records = resourceLeak.ToString();
            if (this.reportedLeaks.TryAdd(records, true))
            {
                if (records.Length == 0)
                {
                    Logger.Error("LEAK: {}.Release() was not called before it's garbage-collected. " +
                        "Enable advanced leak reporting to find out where the leak occurred. " +
                        "To enable advanced leak reporting, " +
                        "set environment variable {} to {} or set {}.Level in code. " +
                        "See http://netty.io/wiki/reference-counted-objects.html for more information.", this.resourceType, PropLevel, DetectionLevel.Advanced.ToString().ToLowerInvariant(), StringUtil.SimpleClassName(this));
                }
                else
                {
                    Logger.Error(
                        "LEAK: {}.release() was not called before it's garbage-collected. " +
                            "See http://netty.io/wiki/reference-counted-objects.html for more information.{}", this.resourceType, records);
                }
            }
        }

        sealed class DefaultResourceLeak : IResourceLeak
        {
            readonly ResourceLeakDetector owner;
            readonly string creationRecord;
            readonly Deque<string> lastRecords = new Deque<string>();
            int freed;

            public DefaultResourceLeak(ResourceLeakDetector owner, object referent)
            {
                this.owner = owner;
                GCNotice existingNotice;
                if (owner.gcNotificationMap.TryGetValue(referent, out existingNotice))
                {
                    existingNotice.Rearm(this);
                }
                else
                {
                    owner.gcNotificationMap.Add(referent, new GCNotice(this));
                }

                if (referent != null)
                {
                    DetectionLevel level = Level;
                    if (level >= DetectionLevel.Advanced)
                    {
                        this.creationRecord = NewRecord(null);
                    }
                    else
                    {
                        this.creationRecord = null;
                    }

                    Interlocked.Increment(ref this.owner.active);
                }
                else
                {
                    this.creationRecord = null;
                    this.freed = 1;
                }
            }

            public void Record() => this.RecordInternal(null);

            public void Record(object hint) => this.RecordInternal(hint);

            void RecordInternal(object hint)
            {
                if (this.creationRecord != null)
                {
                    string value = NewRecord(hint);

                    lock (this.lastRecords)
                    {
                        int size = this.lastRecords.Count;
                        if (size == 0 || this.lastRecords[size - 1].Equals(value))
                        {
                            this.lastRecords.AddToBack(value);
                        }
                        if (size > MaxRecords)
                        {
                            this.lastRecords.RemoveFromFront();
                        }
                    }
                }
            }

            public bool Close()
            {
                if (Interlocked.CompareExchange(ref this.freed, 1, 0) == 0)
                {
                    Interlocked.Decrement(ref this.owner.active);
                    return true;
                }
                return false;
            }

            internal void CloseFinal()
            {
                if (this.Close())
                {
                    this.owner.Report(this);
                }
            }

            public override string ToString()
            {
                if (this.creationRecord == null)
                {
                    return "";
                }

                string[] array;
                lock (this.lastRecords)
                {
                    array = new string[this.lastRecords.Count];
                    ((ICollection<string>)this.lastRecords).CopyTo(array, 0);
                }

                StringBuilder buf = new StringBuilder(16384)
                    .Append(StringUtil.Newline)
                    .Append("Recent access records: ")
                    .Append(array.Length)
                    .Append(StringUtil.Newline);

                if (array.Length > 0)
                {
                    for (int i = array.Length - 1; i >= 0; i--)
                    {
                        buf.Append('#')
                            .Append(i + 1)
                            .Append(':')
                            .Append(StringUtil.Newline)
                            .Append(array[i]);
                    }
                    buf.Append(StringUtil.Newline);
                }

                buf.Append("Created at:")
                    .Append(StringUtil.Newline)
                    .Append(this.creationRecord);

                return buf.ToString();
            }
        }

        static string NewRecord(object hint)
        {
            Contract.Ensures(Contract.Result<string>() != null);

            var buf = new StringBuilder(4096);

            // Append the hint first if available.
            if (hint != null)
            {
                buf.Append("\tHint: ");
                // Prefer a hint string to a simple string form.
                var leakHint = hint as IResourceLeakHint;
                if (leakHint != null)
                {
                    buf.Append(leakHint.ToHintString());
                }
                else
                {
                    buf.Append(hint);
                }
                buf.Append(StringUtil.Newline);
            }

            // Append the stack trace.
            buf.Append(Environment.StackTrace);

            return buf.ToString();
        }

        class GCNotice
        {
            DefaultResourceLeak leak;

            public GCNotice(DefaultResourceLeak leak)
            {
                this.leak = leak;
            }

            ~GCNotice()
            {
                this.leak.CloseFinal();
            }

            public void Rearm(DefaultResourceLeak newLeak)
            {
                DefaultResourceLeak oldLeak = Interlocked.Exchange(ref this.leak, newLeak);
                oldLeak.CloseFinal();
            }
        }
    }
}