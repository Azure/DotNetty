// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public class ThreadLocalPool
    {
        public sealed class Handle
        {
            internal int lastRecycledId;
            internal int recycleId;

            public object Value;
            internal Stack Stack;

            internal Handle(Stack stack)
            {
                this.Stack = stack;
            }

            public void Release<T>(T value)
                where T : class
            {
                Contract.Requires(value == this.Value, "value differs from one backed by this handle.");

                Stack stack = this.Stack;
                Thread thread = Thread.CurrentThread;
                if (stack.Thread == thread)
                {
                    stack.Push(this);
                    return;
                }

                ConditionalWeakTable<Stack, WeakOrderQueue> queueDictionary = DelayedPool.Value;
                WeakOrderQueue delayedRecycled;
                if (!queueDictionary.TryGetValue(stack, out delayedRecycled))
                {
                    var newQueue = new WeakOrderQueue(stack, thread);
                    delayedRecycled = newQueue;
                    queueDictionary.Add(stack, delayedRecycled);
                }
                delayedRecycled.Add(this);
            }
        }

        internal sealed class WeakOrderQueue
        {
            const int LinkCapacity = 16;

            sealed class Link
            {
                int writeIndex;

                internal readonly Handle[] elements;
                internal Link next;

                internal int ReadIndex { get; set; }

                internal int WriteIndex
                {
                    get { return Volatile.Read(ref this.writeIndex); }
                    set { Volatile.Write(ref this.writeIndex, value); }
                }

                internal Link()
                {
                    this.elements = new Handle[LinkCapacity];
                }
            }

            Link head, tail;
            internal WeakOrderQueue next;
            internal WeakReference<Thread> ownerThread;
            readonly int id = Interlocked.Increment(ref idSource);

            internal bool IsEmpty => this.tail.ReadIndex == this.tail.WriteIndex;

            internal WeakOrderQueue(Stack stack, Thread thread)
            {
                Contract.Requires(stack != null);

                this.ownerThread = new WeakReference<Thread>(thread);
                this.head = this.tail = new Link();
                lock (stack)
                {
                    this.next = stack.HeadQueue;
                    stack.HeadQueue = this;
                }
            }

            internal void Add(Handle handle)
            {
                Contract.Requires(handle != null);

                handle.lastRecycledId = this.id;

                Link tail = this.tail;
                int writeIndex = tail.WriteIndex;
                if (writeIndex == LinkCapacity)
                {
                    this.tail = tail = tail.next = new Link();
                    writeIndex = tail.WriteIndex;
                }
                tail.elements[writeIndex] = handle;
                handle.Stack = null;
                tail.WriteIndex = writeIndex + 1;
            }

            internal bool Transfer(Stack dst)
            {
                // This method must be called by owner thread.
                Contract.Requires(dst != null);

                Link head = this.head;
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
                    this.head = head = head.next;
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
                    Handle[] srcElems = head.elements;
                    Handle[] dstElems = dst.elements;
                    int newDstSize = dstSize;
                    for (int i = srcStart; i < srcEnd; i++)
                    {
                        Handle element = srcElems[i];
                        if (element.recycleId == 0)
                        {
                            element.recycleId = element.lastRecycledId;
                        }
                        else if (element.recycleId != element.lastRecycledId)
                        {
                            throw new InvalidOperationException("recycled already");
                        }
                        element.Stack = dst;
                        dstElems[newDstSize++] = element;
                        srcElems[i] = null;
                    }
                    dst.size = newDstSize;

                    if (srcEnd == LinkCapacity && head.next != null)
                    {
                        this.head = head.next;
                    }

                    head.ReadIndex = srcEnd;
                    return true;
                }
                else
                {
                    // The destination stack is full already.
                    return false;
                }
            }
        }

        internal sealed class Stack
        {
            internal readonly ThreadLocalPool Parent;
            internal readonly Thread Thread;

            internal Handle[] elements;

            readonly int maxCapacity;
            internal int size;

            WeakOrderQueue headQueue;
            WeakOrderQueue cursorQueue;
            WeakOrderQueue prevQueue;

            internal WeakOrderQueue HeadQueue
            {
                get { return Volatile.Read(ref this.headQueue); }
                set { Volatile.Write(ref this.headQueue, value); }
            }

            internal int Size => this.size;

            internal Stack(int maxCapacity, ThreadLocalPool parent, Thread thread)
            {
                this.maxCapacity = maxCapacity;
                this.Parent = parent;
                this.Thread = thread;

                this.elements = new Handle[Math.Min(InitialCapacity, maxCapacity)];
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

            internal void Push(Handle item)
            {
                Contract.Requires(item != null);
                if ((item.recycleId | item.lastRecycledId) != 0)
                {
                    throw new InvalidOperationException("released already");
                }
                item.recycleId = item.lastRecycledId = ownThreadId;

                int size = this.size;
                if (size >= this.maxCapacity)
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

            internal bool TryPop(out Handle item)
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
                Handle ret = this.elements[size];
                if (ret.lastRecycledId != ret.recycleId)
                {
                    throw new InvalidOperationException("recycled multiple times");
                }
                ret.recycleId = 0;
                ret.lastRecycledId = 0;
                item = ret;
                this.size = size;

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
                this.cursorQueue = this.HeadQueue;
                return false;
            }

            bool ScavengeSome()
            {
                WeakOrderQueue cursor = this.cursorQueue;
                if (cursor == null)
                {
                    cursor = this.HeadQueue;
                    if (cursor == null)
                    {
                        return false;
                    }
                }

                bool success = false;
                WeakOrderQueue prev = this.prevQueue;
                do
                {
                    if (cursor.Transfer(this))
                    {
                        success = true;
                        break;
                    }

                    WeakOrderQueue next = cursor.next;
                    Thread ownerThread;
                    if (!cursor.ownerThread.TryGetTarget(out ownerThread))
                    {
                        // If the thread associated with the queue is gone, unlink it, after
                        // performing a volatile read to confirm there is no data left to collect.
                        // We never unlink the first queue, as we don't want to synchronize on updating the head.
                        if (!cursor.IsEmpty)
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
                            prev.next = next;
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

        internal static readonly int DefaultMaxCapacity = 262144;
        internal static readonly int InitialCapacity = Math.Min(256, DefaultMaxCapacity);
        static int idSource = int.MinValue;
        static readonly int ownThreadId = Interlocked.Increment(ref idSource);

        internal static readonly DelayedThreadLocal DelayedPool = new DelayedThreadLocal();

        internal sealed class DelayedThreadLocal : FastThreadLocal<ConditionalWeakTable<Stack, WeakOrderQueue>>
        {
            protected override ConditionalWeakTable<Stack, WeakOrderQueue> GetInitialValue() => new ConditionalWeakTable<Stack, WeakOrderQueue>();
        }

        public ThreadLocalPool(int maxCapacity)
        {
            Contract.Requires(maxCapacity > 0);
            this.MaxCapacity = maxCapacity;
        }

        public int MaxCapacity { get; }
    }

    public sealed class ThreadLocalPool<T> : ThreadLocalPool
        where T : class
    {
        readonly ThreadLocalStack threadLocal;
        readonly Func<Handle, T> valueFactory;
        readonly bool preCreate;

        public ThreadLocalPool(Func<Handle, T> valueFactory)
            : this(valueFactory, DefaultMaxCapacity)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacity)
            : this(valueFactory, maxCapacity, false)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacity, bool preCreate)
            : base(maxCapacity)
        {
            Contract.Requires(valueFactory != null);

            this.preCreate = preCreate;

            this.threadLocal = new ThreadLocalStack(this);
            this.valueFactory = valueFactory;
        }

        public T Take()
        {
            Stack stack = this.threadLocal.Value;
            Handle handle;
            if (!stack.TryPop(out handle))
            {
                handle = this.CreateValue(stack);
            }
            return (T)handle.Value;
        }

        Handle CreateValue(Stack stack)
        {
            var handle = new Handle(stack);
            T value = this.valueFactory(handle);
            handle.Value = value;
            return handle;
        }

        internal int ThreadLocalCapacity => this.threadLocal.Value.elements.Length;

        internal int ThreadLocalSize => this.threadLocal.Value.Size;

        sealed class ThreadLocalStack : FastThreadLocal<Stack>
        {
            readonly ThreadLocalPool<T> owner;

            public ThreadLocalStack(ThreadLocalPool<T> owner)
            {
                this.owner = owner;
            }

            protected override Stack GetInitialValue()
            {
                var stack = new Stack(this.owner.MaxCapacity, this.owner, Thread.CurrentThread);
                if (this.owner.preCreate)
                {
                    for (int i = 0; i < this.owner.MaxCapacity; i++)
                    {
                        stack.Push(this.owner.CreateValue(stack));
                    }
                }
                return stack;
            }
        }
    }
}