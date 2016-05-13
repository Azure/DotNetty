// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using DotNetty.Common.Internal;

    sealed class MpscLinkedQueue<T> : MpscLinkedQueueTailRef<T>, IEnumerable<T>, IQueue<T>
        where T : class
    {
#pragma warning disable 169 // padded reference
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p30, p31, p32, p33, p34, p35, p36, p37;
#pragma warning restore 169

        // offer() occurs at the tail of the linked list.
        // poll() occurs at the head of the linked list.
        //
        // Resulting layout is:
        //
        //   head --next--> 1st element --next--> 2nd element --next--> ... tail (last element)
        //
        // where the head is a dummy node whose value is null.
        //
        // offer() appends a new node next to the tail using AtomicReference.getAndSet()
        // poll() removes head from the linked list and promotes the 1st element to the head,
        // setting its value to null if possible.
        public MpscLinkedQueue()
        {
            MpscLinkedQueueNode<T> tombstone = new DefaultNode(null);
            this.HeadRef = tombstone;
            this.TailRef = tombstone;
        }

        /// <summary>
        ///     Returns the node right next to the head, which contains the first element of this queue.
        /// </summary>
        MpscLinkedQueueNode<T> PeekNode()
        {
            MpscLinkedQueueNode<T> head = this.HeadRef;
            MpscLinkedQueueNode<T> next = head.Next;
            if (next == null && head != this.TailRef)
            {
                // if tail != head this is not going to change until consumer makes progress
                // we can avoid reading the head and just spin on next until it shows up
                //
                // See https://github.com/akka/akka/pull/15596
                do
                {
                    next = head.Next;
                }
                while (next == null);
            }
            return next;
        }

        public bool TryEnqueue(T value)
        {
            Contract.Requires(value != null);

            MpscLinkedQueueNode<T> newTail;
            var node = value as MpscLinkedQueueNode<T>;
            if (node != null)
            {
                newTail = node;
                newTail.Next = null;
            }
            else
            {
                newTail = new DefaultNode(value);
            }

            MpscLinkedQueueNode<T> oldTail = this.GetAndSetTailRef(newTail);
            oldTail.Next = newTail;
            return true;
        }

        public T Dequeue()
        {
            MpscLinkedQueueNode<T> next = this.PeekNode();
            if (next == null)
            {
                return null;
            }

            // next becomes a new head.
            MpscLinkedQueueNode<T> oldHead = this.HeadRef;
            // todo: research storestore vs loadstore barriers
            // See: http://robsjava.blogspot.com/2013/06/a-faster-volatile.html
            // See: http://psy-lob-saw.blogspot.com/2012/12/atomiclazyset-is-performance-win-for.html
            this.HeadRef = next;

            // Break the linkage between the old head and the new head.
            oldHead.Unlink();

            return next.ClearMaybe();
        }

        public T Peek()
        {
            MpscLinkedQueueNode<T> next = this.PeekNode();
            return next?.Value;
        }

        public int Count
        {
            get
            {
                int count = 0;
                MpscLinkedQueueNode<T> n = this.PeekNode();
                while (true)
                {
                    if (n == null)
                    {
                        break;
                    }
                    count ++;
                    n = n.Next;
                }
                return count;
            }
        }

        public bool IsEmpty => this.PeekNode() == null;

        public IEnumerator<T> GetEnumerator()
        {
            MpscLinkedQueueNode<T> node = this.PeekNode();
            while (node != null)
            {
                yield return node.Value;
                node = node.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public void Clear()
        {
            while (this.Dequeue() != null)
            {
            }
        }

        class DefaultNode : MpscLinkedQueueNode<T>
        {
            T value;

            internal DefaultNode(T value)
            {
                this.value = value;
            }

            public override T Value => this.value;

            protected internal override T ClearMaybe()
            {
                T v = this.value;
                this.value = null;
                return v;
            }
        }
    }

    abstract class MpscLinkedQueueHeadRef<T> : MpscLinkedQueuePad0
    {
        MpscLinkedQueueNode<T> headRef;

        protected MpscLinkedQueueNode<T> HeadRef
        {
            get { return Volatile.Read(ref this.headRef); }
            set { Volatile.Write(ref this.headRef, value); }
        }
    }

    public abstract class MpscLinkedQueueNode<T>
    {
        volatile MpscLinkedQueueNode<T> next;

        internal MpscLinkedQueueNode<T> Next
        {
            get { return this.next; }
            set { this.next = value; }
        }

        public abstract T Value { get; }

        /// <summary>
        ///     Sets the element this node contains to <code>null</code> so that the node can be used as a tombstone.
        /// </summary>
        /// <returns></returns>
        protected internal virtual T ClearMaybe() => this.Value;

        /// <summary>
        ///     Unlink to allow GC
        /// </summary>
        internal virtual void Unlink() => this.Next = null;
    }

    abstract class MpscLinkedQueuePad0
    {
#pragma warning disable 169 // padded reference
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p30, p31, p32, p33, p34, p35, p36, p37;
#pragma warning restore 169
    }

    abstract class MpscLinkedQueuePad1<T> : MpscLinkedQueueHeadRef<T>
    {
#pragma warning disable 169 // padded reference
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p30, p31, p32, p33, p34, p35, p36, p37;
#pragma warning restore 169
    }

    abstract class MpscLinkedQueueTailRef<T> : MpscLinkedQueuePad1<T>
    {
        MpscLinkedQueueNode<T> tailRef;

        protected MpscLinkedQueueNode<T> TailRef
        {
            get { return Volatile.Read(ref this.tailRef); }
            set { Volatile.Write(ref this.tailRef, value); }
        }

        protected MpscLinkedQueueNode<T> GetAndSetTailRef(MpscLinkedQueueNode<T> value)
        {
#pragma warning disable 420
            return Interlocked.Exchange(ref this.tailRef, value);
#pragma warning restore 420
        }
    }
}