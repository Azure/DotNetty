// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public class ThreadLocalPool
    {
        public abstract class Handle
        {
            public abstract void Release<T>(T value)
                where T : class;
        }

        protected sealed class NoopHandle : Handle
        {
            public static readonly NoopHandle Instance = new NoopHandle();

            NoopHandle()
            {
            }

            public override void Release<T>(T value)
            {
            }
        }

        protected sealed class DefaultHandle : Handle
        {
            internal int lastRecycledId;
            internal int recycleId;

            internal bool hasBeenRecycled;

            internal object Value;
            internal Stack Stack;

            internal DefaultHandle(Stack stack)
            {
                this.Stack = stack;
            }

            public override void Release<T>(T value)
            {
                Contract.Requires(value == this.Value, "value differs from one backed by this handle.");

                Stack stack = this.Stack;
                if (lastRecycledId != recycleId || stack == null)
                {
                    throw new InvalidOperationException("recycled already");
                }
                stack.Push(this);
            }
        }

        // a queue that makes only moderate guarantees about visibility: items are seen in the correct order,
        // but we aren't absolutely guaranteed to ever see anything at all, thereby keeping the queue cheap to maintain
        protected sealed class WeakOrderQueue
        {
            internal static readonly WeakOrderQueue Dummy = new WeakOrderQueue();

            sealed class Link
            {
                private int writeIndex;

                internal readonly DefaultHandle[] elements = new DefaultHandle[LinkCapacity];
                internal Link next;

                internal int ReadIndex { get; set; }

                internal int WriteIndex
                {
                    get => Volatile.Read(ref writeIndex);
                    set => Volatile.Write(ref writeIndex, value);
                }

                internal void LazySetWriteIndex(int value) => writeIndex = value;
            }

            // This act as a place holder for the head Link but also will reclaim space once finalized.
            // Its important this does not hold any reference to either Stack or WeakOrderQueue.
            sealed class Head
            {
                readonly StrongBox<int> availableSharedCapacity;
                readonly StrongBox<int> weakTableCounter;

                internal Link link;

                internal Head(StrongBox<int> availableSharedCapacity, StrongBox<int> weakTableCounter)
                {
                    this.availableSharedCapacity = availableSharedCapacity;
                    this.weakTableCounter = weakTableCounter;
                    if (weakTableCounter != null)
                        Interlocked.Increment(ref weakTableCounter.Value);
                }

                ~Head()
                {
                    if (this.weakTableCounter != null)
                    {
                        Interlocked.Decrement(ref this.weakTableCounter.Value);
                    }
                    if (this.availableSharedCapacity == null)
                    {
                        return;
                    }

                    Link head = this.link;
                    this.link = null;
                    while (head != null)
                    {
                        ReclaimSpace(LinkCapacity);
                        Link next = head.next;
                        // Unlink to help GC and guard against GC nepotism.
                        head.next = null;
                        head = next;
                    }
                }

                internal void ReclaimSpace(int space)
                {
                    Debug.Assert(space >= 0);
                    Interlocked.Add(ref availableSharedCapacity.Value, space);
                }

                internal bool ReserveSpace(int space)
                {
                    return ReserveSpace(availableSharedCapacity, space);
                }

                internal static bool ReserveSpace(StrongBox<int> availableSharedCapacity, int space)
                {
                    Debug.Assert(space >= 0);
                    for (; ; )
                    {
                        int available = Volatile.Read(ref availableSharedCapacity.Value);
                        if (available < space)
                        {
                            return false;
                        }
                        if (Interlocked.CompareExchange(ref availableSharedCapacity.Value, available - space, available) == available)
                        {
                            return true;
                        }
                    }
                }
            }

            // chain of data items
            readonly Head head;
            Link tail;
            // pointer to another queue of delayed items for the same stack
            internal WeakOrderQueue next;
            internal readonly WeakReference<Thread> owner;
            readonly int id = Interlocked.Increment(ref idSource);

            WeakOrderQueue()
            {
                owner = null;
                head = new Head(null, null);
            }

            WeakOrderQueue(Stack stack, Thread thread, DelayedThreadLocal.CountedWeakTable countedWeakTable)
            {
                this.tail = new Link();

                // Its important that we not store the Stack itself in the WeakOrderQueue as the Stack also is used in
                // the WeakHashMap as key. So just store the enclosed AtomicInteger which should allow to have the
                // Stack itself GCed.
                this.head = new Head(stack.availableSharedCapacity, countedWeakTable.Counter);
                this.head.link = tail;
                this.owner = new WeakReference<Thread>(thread);
            }

            static WeakOrderQueue NewQueue(Stack stack, Thread thread, DelayedThreadLocal.CountedWeakTable countedWeakTable)
            {
                WeakOrderQueue queue = new WeakOrderQueue(stack, thread, countedWeakTable);
                // Done outside of the constructor to ensure WeakOrderQueue.this does not escape the constructor and so
                // may be accessed while its still constructed.
                stack.Head = queue;

                return queue;
            }

            internal WeakOrderQueue Next
            {
                set
                {
                    Debug.Assert(value != this);
                    this.next = value;
                }
            }

            /// <summary>
            /// Allocate a new <see cref="WeakOrderQueue"/> or return <code>null</code> if not possible.
            /// </summary>
            internal static WeakOrderQueue Allocate(Stack stack, Thread thread, DelayedThreadLocal.CountedWeakTable countedWeakTable)
            {
                // We allocated a Link so reserve the space
                return Head.ReserveSpace(stack.availableSharedCapacity, LinkCapacity) ? NewQueue(stack, thread, countedWeakTable) : null;
            }

            internal void Add(DefaultHandle handle)
            {
                handle.lastRecycledId = this.id;

                Link tail = this.tail;
                int writeIndex = tail.WriteIndex;
                if (writeIndex == LinkCapacity)
                {
                    if (!head.ReserveSpace(LinkCapacity))
                    {
                        // Drop it.
                        return;
                    }
                    // We allocate a Link so reserve the space
                    this.tail = tail = tail.next = new Link();
                    writeIndex = tail.WriteIndex;
                }
                tail.elements[writeIndex] = handle;
                handle.Stack = null;
                // we lazy set to ensure that setting stack to null appears before we unnull it in the owning thread;
                // this also means we guarantee visibility of an element in the queue if we see the index updated
                tail.LazySetWriteIndex(writeIndex + 1);
            }

            internal bool HasFinalData => this.tail.ReadIndex != this.tail.WriteIndex;

            // transfer as many items as we can from this queue to the stack, returning true if any were transferred
            internal bool Transfer(Stack dst)
            {
                Link head = this.head.link;
                if (head == null)
                {
                    return false;
                }

                if (head.ReadIndex == LinkCapacity)
                {
                    if (head.next == null)
                    {
                        return false;
                    }
                    this.head.link = head = head.next;
                }

                int srcStart = head.ReadIndex;
                int srcEnd = head.WriteIndex;
                int srcSize = srcEnd - srcStart;
                if (srcSize == 0)
                {
                    return false;
                }

                int dstSize = dst.size;
                int expectedCapacity = dstSize + srcSize;

                if (expectedCapacity > dst.elements.Length)
                {
                    int actualCapacity = dst.IncreaseCapacity(expectedCapacity);
                    srcEnd = Math.Min(srcStart + actualCapacity - dstSize, srcEnd);
                }

                if (srcStart != srcEnd)
                {
                    DefaultHandle[] srcElems = head.elements;
                    DefaultHandle[] dstElems = dst.elements;
                    int newDstSize = dstSize;
                    for (int i = srcStart; i < srcEnd; i++)
                    {
                        DefaultHandle element = srcElems[i];
                        if (element.recycleId == 0)
                        {
                            element.recycleId = element.lastRecycledId;
                        }
                        else if (element.recycleId != element.lastRecycledId)
                        {
                            throw new InvalidOperationException("recycled already");
                        }
                        srcElems[i] = null;

                        if (dst.DropHandle(element))
                        {
                            // Drop the object.
                            continue;
                        }
                        element.Stack = dst;
                        dstElems[newDstSize++] = element;
                    }

                    if (srcEnd == LinkCapacity && head.next != null)
                    {
                        // Add capacity back as the Link is GCed.
                        this.head.ReclaimSpace(LinkCapacity);
                        this.head.link = head.next;
                    }

                    head.ReadIndex = srcEnd;
                    if (dst.size == newDstSize)
                    {
                        return false;
                    }
                    dst.size = newDstSize;
                    return true;
                }
                else
                {
                    // The destination stack is full already.
                    return false;
                }
            }
        }

        protected sealed class Stack
        {
            // we keep a queue of per-thread queues, which is appended to once only, each time a new thread other
            // than the stack owner recycles: when we run out of items in our stack we iterate this collection
            // to scavenge those that can be reused. this permits us to incur minimal thread synchronisation whilst
            // still recycling all items.
            internal readonly ThreadLocalPool parent;

            // We store the Thread in a WeakReference as otherwise we may be the only ones that still hold a strong
            // Reference to the Thread itself after it died because DefaultHandle will hold a reference to the Stack.
            //
            // The biggest issue is if we do not use a WeakReference the Thread may not be able to be collected at all if
            // the user will store a reference to the DefaultHandle somewhere and never clear this reference (or not clear
            // it in a timely manner).
            internal readonly WeakReference<Thread> threadRef;
            internal readonly StrongBox<int> availableSharedCapacity;
            internal readonly int maxDelayedQueues;

            readonly int maxCapacity;
            readonly int ratioMask;
            internal DefaultHandle[] elements;
            internal int size;
            int handleRecycleCount = -1; // Start with -1 so the first one will be recycled.
            WeakOrderQueue cursorQueue, prevQueue;
            volatile WeakOrderQueue headQueue;

            internal Stack(ThreadLocalPool parent, Thread thread, int maxCapacity, int maxSharedCapacityFactor,
                int ratioMask, int maxDelayedQueues)
            {
                this.parent = parent;
                this.threadRef = new WeakReference<Thread>(thread);
                this.maxCapacity = maxCapacity;
                this.availableSharedCapacity = new StrongBox<int>(Math.Max(maxCapacity / maxSharedCapacityFactor, LinkCapacity));
                this.elements = new DefaultHandle[Math.Min(DefaultInitialCapacity, maxCapacity)];
                this.ratioMask = ratioMask;
                this.maxDelayedQueues = maxDelayedQueues;
            }

            internal WeakOrderQueue Head
            {
                set
                {
                    lock (this)
                    {
                        value.next = headQueue;
                        headQueue = value;
                    }
                }
            }

            internal int IncreaseCapacity(int expectedCapacity)
            {
                int newCapacity = this.elements.Length;
                int maxCapacity = this.maxCapacity;
                do
                {
                    newCapacity <<= 1;
                }
                while (newCapacity < expectedCapacity && newCapacity < maxCapacity);

                newCapacity = Math.Min(newCapacity, maxCapacity);
                if (newCapacity != this.elements.Length)
                {
                    Array.Resize(ref this.elements, newCapacity);
                }

                return newCapacity;
            }

            internal void Push(DefaultHandle item)
            {
                Thread currentThread = Thread.CurrentThread;
                if (threadRef.TryGetTarget(out Thread thread) && thread == currentThread)
                {
                    // The current Thread is the thread that belongs to the Stack, we can try to push the object now.
                    PushNow(item);
                }
                else
                {
                    // The current Thread is not the one that belongs to the Stack
                    // (or the Thread that belonged to the Stack was collected already), we need to signal that the push
                    // happens later.
                    PushLater(item, currentThread);
                }
            }

            void PushNow(DefaultHandle item)
            {
                if ((item.recycleId | item.lastRecycledId) != 0)
                {
                    throw new InvalidOperationException("released already");
                }
                item.recycleId = item.lastRecycledId = ownThreadId;

                int size = this.size;
                if (size >= this.maxCapacity || DropHandle(item))
                {
                    // Hit the maximum capacity - drop the possibly youngest object.
                    return;
                }
                if (size == this.elements.Length)
                {
                    Array.Resize(ref this.elements, Math.Min(size << 1, this.maxCapacity));
                }

                this.elements[size] = item;
                this.size = size + 1;
            }

            void PushLater(DefaultHandle item, Thread thread)
            {
                // we don't want to have a ref to the queue as the value in our weak map
                // so we null it out; to ensure there are no races with restoring it later
                // we impose a memory ordering here (no-op on x86)
                DelayedThreadLocal.CountedWeakTable countedWeakTable = DelayedPool.Value;
                ConditionalWeakTable<Stack, WeakOrderQueue> delayedRecycled = countedWeakTable.WeakTable;
                delayedRecycled.TryGetValue(this, out WeakOrderQueue queue);
                if (queue == null)
                {
                    if (Volatile.Read(ref countedWeakTable.Counter.Value) >= maxDelayedQueues)
                    {
                        // Add a dummy queue so we know we should drop the object
                        delayedRecycled.Add(this, WeakOrderQueue.Dummy);
                        return;
                    }
                    // Check if we already reached the maximum number of delayed queues and if we can allocate at all.
                    if ((queue = WeakOrderQueue.Allocate(this, thread, countedWeakTable)) == null)
                    {
                        // drop object
                        return;
                    }
                    delayedRecycled.Add(this, queue);
                }
                else if (queue == WeakOrderQueue.Dummy)
                {
                    // drop object
                    return;
                }

                queue.Add(item);
            }

            internal bool DropHandle(DefaultHandle handle)
            {
                if (!handle.hasBeenRecycled)
                {
                    if ((++handleRecycleCount & ratioMask) != 0)
                    {
                        // Drop the object.
                        return true;
                    }
                    handle.hasBeenRecycled = true;
                }
                return false;
            }

            internal DefaultHandle NewHandle() => new DefaultHandle(this);

            internal bool TryPop(out DefaultHandle item)
            {
                int size = this.size;
                if (size == 0)
                {
                    if (!this.Scavenge())
                    {
                        item = null;
                        return false;
                    }
                    size = this.size;
                }
                size--;
                DefaultHandle ret = this.elements[size];
                elements[size] = null;
                if (ret.lastRecycledId != ret.recycleId)
                {
                    throw new InvalidOperationException("recycled multiple times");
                }
                ret.recycleId = 0;
                ret.lastRecycledId = 0;
                this.size = size;

                item = ret;
                return true;
            }

            bool Scavenge()
            {
                // continue an existing scavenge, if any
                if (this.ScavengeSome())
                {
                    return true;
                }

                // reset our scavenge cursor
                this.prevQueue = null;
                this.cursorQueue = this.headQueue;
                return false;
            }

            bool ScavengeSome()
            {
                WeakOrderQueue prev;
                WeakOrderQueue cursor = this.cursorQueue;
                if (cursor == null)
                {
                    prev = null;
                    cursor = this.headQueue;
                    if (cursor == null)
                    {
                        return false;
                    }
                }
                else
                {
                    prev = this.prevQueue;
                }

                bool success = false;
                do
                {
                    if (cursor.Transfer(this))
                    {
                        success = true;
                        break;
                    }

                    WeakOrderQueue next = cursor.next;
                    if (!cursor.owner.TryGetTarget(out _))
                    {
                        // If the thread associated with the queue is gone, unlink it, after
                        // performing a volatile read to confirm there is no data left to collect.
                        // We never unlink the first queue, as we don't want to synchronize on updating the head.
                        if (cursor.HasFinalData)
                        {
                            for (;;)
                            {
                                if (cursor.Transfer(this))
                                {
                                    success = true;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        if (prev != null)
                        {
                            prev.Next = next;
                        }
                    }
                    else
                    {
                        prev = cursor;
                    }

                    cursor = next;
                }
                while (cursor != null && !success);

                this.prevQueue = prev;
                this.cursorQueue = cursor;
                return success;
            }
        }

        const int DefaultInitialMaxCapacityPerThread = 4 * 1024; // Use 4k instances as default.
        protected static readonly int DefaultMaxCapacityPerThread;
        protected static readonly int DefaultInitialCapacity;
        protected static readonly int DefaultMaxSharedCapacityFactor;
        protected static readonly int DefaultMaxDelayedQueuesPerThread;
        protected static readonly int LinkCapacity;
        protected static readonly int DefaultRatio;
        static int idSource = int.MinValue;
        static readonly int ownThreadId = Interlocked.Increment(ref idSource);

        protected static readonly DelayedThreadLocal DelayedPool = new DelayedThreadLocal();

        protected sealed class DelayedThreadLocal : FastThreadLocal<DelayedThreadLocal.CountedWeakTable>
        {
            public class CountedWeakTable
            {
                internal readonly ConditionalWeakTable<Stack, WeakOrderQueue> WeakTable = new ConditionalWeakTable<Stack, WeakOrderQueue>();

                internal readonly StrongBox<int> Counter = new StrongBox<int>();
            }
            protected override CountedWeakTable GetInitialValue() => new CountedWeakTable();
        }

        static ThreadLocalPool()
        {
            // In the future, we might have different maxCapacity for different object types.
            // e.g. io.netty.recycler.maxCapacity.writeTask
            //      io.netty.recycler.maxCapacity.outboundBuffer
            int maxCapacityPerThread = SystemPropertyUtil.GetInt("io.netty.recycler.maxCapacityPerThread",
                    SystemPropertyUtil.GetInt("io.netty.recycler.maxCapacity", DefaultInitialMaxCapacityPerThread));
            if (maxCapacityPerThread < 0)
            {
                maxCapacityPerThread = DefaultInitialMaxCapacityPerThread;
            }

            DefaultMaxCapacityPerThread = maxCapacityPerThread;

            DefaultMaxSharedCapacityFactor = Math.Max(2,
                    SystemPropertyUtil.GetInt("io.netty.recycler.maxSharedCapacityFactor",
                            2));

            DefaultMaxDelayedQueuesPerThread = Math.Max(0,
                    SystemPropertyUtil.GetInt("io.netty.recycler.maxDelayedQueuesPerThread",
                            // We use the same value as default EventLoop number
                            Environment.ProcessorCount * 2));

            LinkCapacity = MathUtil.SafeFindNextPositivePowerOfTwo(
                    Math.Max(SystemPropertyUtil.GetInt("io.netty.recycler.linkCapacity", 16), 16));

            // By default we allow one push to a Recycler for each 8th try on handles that were never recycled before.
            // This should help to slowly increase the capacity of the recycler while not be too sensitive to allocation
            // bursts.
            DefaultRatio = MathUtil.SafeFindNextPositivePowerOfTwo(SystemPropertyUtil.GetInt("io.netty.recycler.ratio", 8));

            IInternalLogger logger = InternalLoggerFactory.GetInstance(typeof(ThreadLocalPool));
            if (logger.DebugEnabled)
            {
                if (DefaultMaxCapacityPerThread == 0)
                {
                    logger.Debug("-Dio.netty.recycler.maxCapacityPerThread: disabled");
                    logger.Debug("-Dio.netty.recycler.maxSharedCapacityFactor: disabled");
                    logger.Debug("-Dio.netty.recycler.maxDelayedQueuesPerThread: disabled");
                    logger.Debug("-Dio.netty.recycler.linkCapacity: disabled");
                    logger.Debug("-Dio.netty.recycler.ratio: disabled");
                }
                else
                {
                    logger.Debug("-Dio.netty.recycler.maxCapacityPerThread: {}", DefaultMaxCapacityPerThread);
                    logger.Debug("-Dio.netty.recycler.maxSharedCapacityFactor: {}", DefaultMaxSharedCapacityFactor);
                    logger.Debug("-Dio.netty.recycler.maxDelayedQueuesPerThread: {}", DefaultMaxDelayedQueuesPerThread);
                    logger.Debug("-Dio.netty.recycler.linkCapacity: {}", LinkCapacity);
                    logger.Debug("-Dio.netty.recycler.ratio: {}", DefaultRatio);
                }
            }

            DefaultInitialCapacity = Math.Min(DefaultMaxCapacityPerThread, 256);
        }

        public ThreadLocalPool(int maxCapacityPerThread)
            : this (maxCapacityPerThread, DefaultMaxSharedCapacityFactor, DefaultRatio, DefaultMaxDelayedQueuesPerThread)
        {
        }

        public ThreadLocalPool(int maxCapacityPerThread, int maxSharedCapacityFactor,
                       int ratio, int maxDelayedQueuesPerThread)
        {
            this.ratioMask = MathUtil.SafeFindNextPositivePowerOfTwo(ratio) - 1;
            if (maxCapacityPerThread <= 0)
            {
                this.maxCapacityPerThread = 0;
                this.maxSharedCapacityFactor = 1;
                this.maxDelayedQueuesPerThread = 0;
            }
            else
            {
                this.maxCapacityPerThread = maxCapacityPerThread;
                this.maxSharedCapacityFactor = Math.Max(1, maxSharedCapacityFactor);
                this.maxDelayedQueuesPerThread = Math.Max(0, maxDelayedQueuesPerThread);
            }
        }

        protected readonly int maxCapacityPerThread;
        protected readonly int ratioMask;
        protected readonly int maxSharedCapacityFactor;
        protected readonly int maxDelayedQueuesPerThread;
    }

    public sealed class ThreadLocalPool<T> : ThreadLocalPool
        where T : class
    {
        readonly ThreadLocalStack threadLocal;
        readonly bool preCreate;
        readonly Func<Handle, T> valueFactory;

        public ThreadLocalPool(Func<Handle, T> valueFactory)
            : this(valueFactory, DefaultMaxCapacityPerThread)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacityPerThread)
            : this(valueFactory, maxCapacityPerThread, DefaultMaxCapacityPerThread, DefaultRatio, DefaultMaxCapacityPerThread, false)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacityPerThread, bool preCreate)
            : this(valueFactory, maxCapacityPerThread, DefaultMaxCapacityPerThread, DefaultRatio, DefaultMaxCapacityPerThread, false)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacityPerThread, int maxSharedCapacityFactor)
            : this(valueFactory, maxCapacityPerThread, maxSharedCapacityFactor, DefaultRatio, DefaultMaxCapacityPerThread, false)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacityPerThread, int maxSharedCapacityFactor,
                       int ratio, int maxDelayedQueuesPerThread, bool preCreate = false)
            : base(maxCapacityPerThread, maxSharedCapacityFactor, ratio, maxDelayedQueuesPerThread)
        {
            Contract.Requires(valueFactory != null);

            this.preCreate = preCreate;

            this.threadLocal = new ThreadLocalStack(this);
            this.valueFactory = valueFactory;
        }

        public T Take()
        {
            if (maxCapacityPerThread == 0)
            {
                return this.valueFactory(NoopHandle.Instance);
            }

            Stack stack = this.threadLocal.Value;
            DefaultHandle handle;
            if (!stack.TryPop(out handle))
            {
                handle = CreateValue(stack);
            }
            return (T)handle.Value;
        }

        DefaultHandle CreateValue(Stack stack)
        {
            var handle = stack.NewHandle();
            handle.Value = this.valueFactory(handle);
            return handle;
        }

        internal int ThreadLocalCapacity => this.threadLocal.Value.elements.Length;

        internal int ThreadLocalSize => this.threadLocal.Value.size;

        sealed class ThreadLocalStack : FastThreadLocal<Stack>
        {
            readonly ThreadLocalPool<T> owner;

            public ThreadLocalStack(ThreadLocalPool<T> owner)
            {
                this.owner = owner;
            }

            protected override Stack GetInitialValue()
            {
                var stack = new Stack(this.owner, Thread.CurrentThread, this.owner.maxCapacityPerThread,
                        this.owner.maxSharedCapacityFactor, this.owner.ratioMask, this.owner.maxDelayedQueuesPerThread);
                if (this.owner.preCreate)
                {
                    for (int i = 0; i < this.owner.maxCapacityPerThread; i++)
                    {
                        stack.Push(this.owner.CreateValue(stack));
                    }
                }
                return stack;
            }

            protected override void OnRemoval(Stack value)
            {
                // Let us remove the WeakOrderQueue from the WeakHashMap directly if its safe to remove some overhead
                if (value.threadRef.TryGetTarget(out Thread valueThread) && valueThread == Thread.CurrentThread)
                {
                    if (DelayedPool.IsSet())
                    {
                        DelayedPool.Value.WeakTable.Remove(value);
                    }
                }
            }
        }
    }
}