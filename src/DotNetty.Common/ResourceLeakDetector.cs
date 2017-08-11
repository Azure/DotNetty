// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
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

        /// <summary>
        ///    Represents the level of resource leak detection.
        /// </summary>
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
            if (!Enum.TryParse(levelStr, true, out DetectionLevel level))
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

        // Should be power of two.
        const int DefaultSamplingInterval = 128;

        /// Returns <c>true</c> if resource leak detection is enabled.
        public static bool Enabled => Level > DetectionLevel.Disabled;

        /// <summary>
        ///     Gets or sets resource leak detection level
        /// </summary>
        public static DetectionLevel Level { get; set; }

        readonly ConditionalWeakTable<object, GCNotice> gcNotificationMap = new ConditionalWeakTable<object, GCNotice>();
        readonly ConcurrentDictionary<string, bool> reportedLeaks = new ConcurrentDictionary<string, bool>();

        readonly string resourceType;
        readonly int samplingInterval;

        public ResourceLeakDetector(string resourceType)
            : this(resourceType, DefaultSamplingInterval)
        {
        }

        public ResourceLeakDetector(string resourceType, int samplingInterval)
        {
            Contract.Requires(resourceType != null);
            Contract.Requires(samplingInterval > 0);

            this.resourceType = resourceType;
            this.samplingInterval = samplingInterval;
        }

        public static ResourceLeakDetector Create<T>() => new ResourceLeakDetector(StringUtil.SimpleClassName<T>());

        /// <summary>
        ///     Creates a new <see cref="IResourceLeakTracker" /> which is expected to be closed
        ///     when the
        ///     related resource is deallocated.
        /// </summary>
        /// <returns>the <see cref="IResourceLeakTracker" /> or <c>null</c></returns>
        public IResourceLeakTracker Track(object obj)
        {
            DetectionLevel level = Level;
            if (level == DetectionLevel.Disabled)
            {
                return null;
            }
            if (level < DetectionLevel.Paranoid)
            {
                if ((PlatformDependent.GetThreadLocalRandom().Next(this.samplingInterval)) == 0)
                {
                    return new DefaultResourceLeak(this, obj);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return new DefaultResourceLeak(this, obj);
            }
        }

        internal void Report(IResourceLeakTracker resourceLeak)
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

        sealed class DefaultResourceLeak : IResourceLeakTracker
        {
            readonly ResourceLeakDetector owner;
            readonly string creationRecord;
            readonly Deque<string> lastRecords = new Deque<string>();
            int removedRecords;

            public DefaultResourceLeak(ResourceLeakDetector owner, object referent)
            {
                Debug.Assert(referent != null);

                this.owner = owner;
                if (owner.gcNotificationMap.TryGetValue(referent, out GCNotice existingNotice))
                {
                    existingNotice.Rearm(this);
                }
                else
                {
                    owner.gcNotificationMap.Add(referent, new GCNotice(this, referent));
                }

                DetectionLevel level = Level;
                if (level >= DetectionLevel.Advanced)
                {
                    this.creationRecord = NewRecord(null);
                }
                else
                {
                    this.creationRecord = null;
                }
            }

            public void Record() => this.RecordInternal(null);

            public void Record(object hint) => this.RecordInternal(hint);

            void RecordInternal(object hint)
            {
                if (this.creationRecord != null && MaxRecords > 0)
                {
                    string value = NewRecord(hint);

                    lock (this.lastRecords)
                    {
                        int size = this.lastRecords.Count;
                        if (size == 0 || this.lastRecords[size - 1].Equals(value))
                        {
                            if (size > MaxRecords)
                            {
                                this.lastRecords.RemoveFromFront();
                                ++this.removedRecords;
                            }
                            this.lastRecords.AddToBack(value);
                        }
                    }
                }
            }

            public bool Close(object trackedObject)
            {
                return this.owner.gcNotificationMap.Remove(trackedObject);
            }

            internal void CloseFinal(object trackedObject)
            {
                if (this.Close(trackedObject))
                {
                    this.owner.Report(this);
                }
            }

            public override string ToString()
            {
                if (this.creationRecord == null)
                {
                    return string.Empty;
                }

                string[] array;
                int removed;
                lock (this.lastRecords)
                {
                    array = new string[this.lastRecords.Count];
                    ((ICollection<string>)this.lastRecords).CopyTo(array, 0);
                    removed = this.removedRecords;
                }

                StringBuilder buf = new StringBuilder(16384).Append(StringUtil.Newline);
                if (removed > 0)
                {
                    buf.Append("WARNING: ")
                        .Append(removed)
                        .Append(" leak records were discarded because the leak record count is limited to ")
                        .Append(MaxRecords)
                        .Append(". Use system property ")
                        .Append(PropMaxRecords)
                        .Append(" to increase the limit.")
                        .Append(StringUtil.Newline);
                }

                buf.Append("Recent access records: ")
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
            // ConditionalWeakTable
            //
            // Lifetimes of keys and values:
            //
            //    Inserting a key and value into the dictonary will not
            //    prevent the key from dying, even if the key is strongly reachable
            //    from the value.
            //
            //    Prior to ConditionalWeakTable, the CLR did not expose
            //    the functionality needed to implement this guarantee.
            //
            //    Once the key dies, the dictionary automatically removes
            //    the key/value entry.
            //
            DefaultResourceLeak leak;
            object referent;

            public GCNotice(DefaultResourceLeak leak, object referent)
            {
                this.leak = leak;
                this.referent = referent;
            }

            ~GCNotice()
            {
                object trackedObject = this.referent;
                this.referent = null;
                this.leak.CloseFinal(trackedObject);
            }

            public void Rearm(DefaultResourceLeak newLeak)
            {
                DefaultResourceLeak oldLeak = Interlocked.Exchange(ref this.leak, newLeak);
                oldLeak.CloseFinal(this.referent);
            }
        }
    }
}