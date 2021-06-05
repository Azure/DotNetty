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

    public class ResourceLeakDetector
    {
        const string PropLevel = "io.netty.leakDetection.level";
        const DetectionLevel DefaultLevel = DetectionLevel.Simple;

        const string PropTargetRecords = "io.netty.leakDetection.targetRecords";
        const int DefaultTargetRecords = 4;

        static readonly int TargetRecords;

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

            TargetRecords = SystemPropertyUtil.GetInt(PropTargetRecords, DefaultTargetRecords);
            Level = level;

            if (Logger.DebugEnabled)
            {
                Logger.Debug("-D{}: {}", PropLevel, level.ToString().ToLower());
                Logger.Debug("-D{}: {}", PropTargetRecords, TargetRecords);
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

        void ReportLeak(DefaultResourceLeak resourceLeak)
        {
            if (!Logger.ErrorEnabled)
            {
                resourceLeak.Dispose();
                return;
            }

            string records = resourceLeak.Dump();
            if (this.reportedLeaks.TryAdd(records, true))
            {
                if (records.Length == 0)
                {
                    this.ReportUntracedLeak(this.resourceType);
                }
                else
                {
                    this.ReportTracedLeak(this.resourceType, records);
                }
            }
        }

        protected void ReportTracedLeak(string type, string records)
        {
            Logger.Error(
                "LEAK: {}.Release() was not called before it's garbage-collected. " +
                "See http://netty.io/wiki/reference-counted-objects.html for more information.{}",
                type, records);
        }

        protected void ReportUntracedLeak(string type)
        {
            Logger.Error("LEAK: {}.release() was not called before it's garbage-collected. " +
                "Enable advanced leak reporting to find out where the leak occurred. " +
                "To enable advanced leak reporting, " +
                "specify the JVM option '-D{}={}' or call {}.setLevel() " +
                "See http://netty.io/wiki/reference-counted-objects.html for more information.",
                type, PropLevel, DetectionLevel.Advanced.ToString().ToLower(), StringUtil.SimpleClassName(this));
        }

        sealed class DefaultResourceLeak : IResourceLeakTracker
        {
            readonly ResourceLeakDetector owner;

            RecordEntry head;
            long droppedRecords;
            readonly WeakReference<GCNotice> gcNotice;

            public DefaultResourceLeak(ResourceLeakDetector owner, object referent)
            {
                Debug.Assert(referent != null);

                this.owner = owner;
                GCNotice gcNotice;
                do
                {
                    GCNotice gcNotice0 = null;
                    gcNotice = owner.gcNotificationMap.GetValue(referent, referent0 =>
                    {
                        gcNotice0 = new GCNotice(referent0, owner);
                        return gcNotice0;
                    });
                    if (gcNotice0 != null && gcNotice0 != gcNotice)
                    {
                        GC.SuppressFinalize(gcNotice0);
                    }
                }
                while (!gcNotice.Arm(this, owner, referent));
                this.gcNotice = new WeakReference<GCNotice>(gcNotice);
                this.head = RecordEntry.Bottom;
                Record();
            }

            public void Record() => this.Record0(null);

            public void Record(object hint) => this.Record0(hint);

            void Record0(object hint)
            {
                // Check TARGET_RECORDS > 0 here to avoid similar check before remove from and add to lastRecords
                if (TargetRecords > 0)
                {
                    string stackTrace = Environment.StackTrace;

                    RecordEntry oldHead;
                    RecordEntry prevHead;
                    RecordEntry newHead;
                    bool dropped;
                    do
                    {
                        if ((prevHead = oldHead = Volatile.Read(ref this.head)) == null)
                        {
                            // already closed.
                            return;
                        }
                        int numElements = oldHead.Pos + 1;
                        if (numElements >= TargetRecords)
                        {
                            int backOffFactor = Math.Min(numElements - TargetRecords, 30);
                            dropped = PlatformDependent.GetThreadLocalRandom().Next(1 << backOffFactor) != 0;
                            if (dropped)
                            {
                                prevHead = oldHead.Next;
                            }
                        }
                        else
                        {
                            dropped = false;
                        }
                        newHead = hint != null ? new RecordEntry(prevHead, stackTrace, hint) : new RecordEntry(prevHead, stackTrace);
                    }
                    while (Interlocked.CompareExchange(ref this.head, newHead, oldHead) != oldHead);
                    if (dropped)
                    {
                        Interlocked.Increment(ref this.droppedRecords);
                    }
                }
            }

            public bool Close(object trackedObject)
            {
                if (gcNotice.TryGetTarget(out var notice))
                {
                    if (notice.UnArm(this, owner, trackedObject))
                    {
                        this.Dispose();
                        return true;
                    }
                }

                return false;
            }

            // This is called from GCNotice finalizer
            internal void CloseFinal()
            {
                if (Volatile.Read(ref this.head) != null)
                {
                    this.owner.ReportLeak(this);
                }
            }

            public string Dump()
            {
                RecordEntry oldHead = Interlocked.Exchange(ref this.head, null);
                if (oldHead == null)
                {
                    // Already closed
                    return string.Empty;
                }

                long dropped = Interlocked.Read(ref this.droppedRecords);
                int duped = 0;

                int present = oldHead.Pos + 1;
                // Guess about 2 kilobytes per stack trace
                var buf = new StringBuilder(present * 2048);
                buf.Append(StringUtil.Newline);
                buf.Append("Recent access records: ").Append(StringUtil.Newline);

                int i = 1;
                var seen = new HashSet<string>();
                for (; oldHead != RecordEntry.Bottom; oldHead = oldHead.Next)
                {
                    string s = oldHead.ToString();
                    if (seen.Add(s))
                    {
                        if (oldHead.Next == RecordEntry.Bottom)
                        {
                            buf.Append("Created at:").Append(StringUtil.Newline).Append(s);
                        }
                        else
                        {
                            buf.Append('#').Append(i++).Append(':').Append(StringUtil.Newline).Append(s);
                        }
                    }
                    else
                    {
                        duped++;
                    }
                }

                if (duped > 0)
                {
                    buf.Append(": ")
                        .Append(duped)
                        .Append(" leak records were discarded because they were duplicates")
                        .Append(StringUtil.Newline);
                }

                if (dropped > 0)
                {
                    buf.Append(": ")
                        .Append(dropped)
                        .Append(" leak records were discarded because the leak record count is targeted to ")
                        .Append(TargetRecords)
                        .Append(". Use system property ")
                        .Append(PropTargetRecords)
                        .Append(" to increase the limit.")
                        .Append(StringUtil.Newline);
                }

                buf.Length = buf.Length - StringUtil.Newline.Length;
                return buf.ToString();
            }

            internal void Dispose()
            {
                Interlocked.Exchange(ref this.head, null);
            }
        }

        // Record
        sealed class RecordEntry
        {
            internal static readonly RecordEntry Bottom = new RecordEntry();

            readonly string hintString;
            internal readonly RecordEntry Next;
            internal readonly int Pos;
            readonly string stackTrace;

            internal RecordEntry(RecordEntry next, string stackTrace, object hint)
            {
                // This needs to be generated even if toString() is never called as it may change later on.
                this.hintString = hint is IResourceLeakHint leakHint ? leakHint.ToHintString() : null;
                this.Next = next;
                this.Pos = next.Pos + 1;
                this.stackTrace = stackTrace;
            }

            internal RecordEntry(RecordEntry next, string stackTrace)
            {
                this.hintString = null;
                this.Next = next;
                this.Pos = next.Pos + 1;
                this.stackTrace = stackTrace;
            }

            // Used to terminate the stack
            RecordEntry()
            {
                this.hintString = null;
                this.Next = null;
                this.Pos = -1;
                this.stackTrace = string.Empty;
            }

            public override string ToString()
            {
                var buf = new StringBuilder(2048);
                if (this.hintString != null)
                {
                    buf.Append("\tHint: ").Append(this.hintString).Append(StringUtil.Newline);
                }

                // TODO: Use StackTrace class and support excludedMethods NETStandard2.0
                // Append the stack trace.
                buf.Append(this.stackTrace).Append(StringUtil.Newline);
                return buf.ToString();
            }
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
            private readonly LinkedList<DefaultResourceLeak> leakList = new LinkedList<DefaultResourceLeak>();
            object referent;
            ResourceLeakDetector owner;
            public GCNotice(object referent, ResourceLeakDetector owner)
            {
                this.referent = referent;
                this.owner = owner;
            }

            ~GCNotice()
            {
                lock (this.leakList)
                {
                    foreach (var leak in this.leakList)
                    {
                        leak.CloseFinal();
                    }
                    this.leakList.Clear();

                    //Since we get here with finalizer, it's no needed to remove key from gcNotificationMap

                    //this.referent = null;
                    this.owner = null;
                }
            }

            public bool Arm(DefaultResourceLeak leak, ResourceLeakDetector owner, object referent)
            {
                lock (this.leakList)
                {
                    if (this.owner == null)
                    {
                        //Already disposed
                        return false;
                    }
                    Debug.Assert(owner == this.owner);
                    Debug.Assert(referent == this.referent);

                    this.leakList.AddLast(leak);
                    return true;
                }
            }

            public bool UnArm(DefaultResourceLeak leak, ResourceLeakDetector owner, object referent)
            {
                lock (this.leakList)
                {
                    if (this.owner == null)
                    {
                        //Already disposed
                        return false;
                    }
                    Debug.Assert(owner == this.owner);
                    Debug.Assert(referent == this.referent);

                    bool res = this.leakList.Remove(leak);
                    if (this.leakList.Count == 0)
                    {
                        // The close is called by byte buffer release, in this case
                        // we suppress the GCNotice finalize to prevent false positive
                        // report where the byte buffer instance gets reused by thread
                        // local cache and the existing GCNotice finalizer still holds
                        // the same byte buffer instance.
                        GC.SuppressFinalize(this);

                        // Don't inline the variable, anything inside Debug.Assert()
                        // will be stripped out in Release builds
                        bool removed = this.owner.gcNotificationMap.Remove(this.referent);
                        Debug.Assert(removed);

                        //this.referent = null;
                        this.owner = null;
                    }
                    return res;
                }
            }
        }
    }
}