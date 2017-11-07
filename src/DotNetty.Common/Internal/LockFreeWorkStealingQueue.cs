// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Represents a hybrid queue, that uses non blocking (spining) techniques to achive thread safety.
    /// </summary>
    /// <remarks>
    /// The queue is hybrid due it's functionality that it can dequeue last node, which makes it
    /// perfect for certain set of alghoritms, like work stealing.
    /// </remarks>
    /// <typeparam name="T">generic Typeparam.</typeparam>
    public class LockFreeWorkStealingQueue<T> : IDeque<T>, IEnumerable<T>
    {
        /// <summary>
        /// Internal node class for the use of internal double linked list structure.
        /// </summary>
        class Node
        {
            public T val;
            public volatile Node next;
            public volatile Node prev;
            public int id;
        }

        static readonly Predicate<int> FalsePredicate = _ => false;

        /*
        * You may ask yourself why volatile is here, well the
        * main reason for this when we don't do explicit locking
        * we don't get the memory barier safety so instructions
        * might get reordered.
        *
        * NOTE: having volatile code in here is just a workaround
        * as we get a performance hit, instead we need to put mem bariers
        * when they are actually needed!
        */
        volatile Node head;

        volatile Node tail;

        /// <summary>
        /// Initializes a new instance of a hybrid queue.
        /// </summary>
        public LockFreeWorkStealingQueue()
        {
            this.head = new Node();
            this.tail = new Node();
            this.head = this.tail;
        }

        /// <summary>
        /// Gets the Unsafe Count (A count that will not nesserly provide the correct actual value).
        /// </summary>
        /// <remarks>
        /// This property is very handy when trying to issue a steal depending on a certain window.
        /// </remarks>
        public int UnsafeCount => this.tail.id - this.head.id;

        /// <summary>
        /// Gets the count.
        /// </summary>
        public int Count
        {
            get
            {
                this.EvaluateCount(FalsePredicate, out int count);
                return count;
            }
        }

        /// <summary>
        /// Stars counting nodes utils a certain condition has been met.
        /// </summary>
        /// <param name="value">the confiiton.</param>
        /// <returns>the value indication that the condition was met or not.</returns>
        public bool EvaluateCount(Predicate<int> value) => this.EvaluateCount(value, out _);

        /// <summary>
        /// Stars counting nodes utils a certain condition has been met.
        /// </summary>
        /// <param name="value">the confiiton.</param>
        /// <param name="actualCount">the actual counted number of elements.</param>
        /// <returns>the value indication that the condition was met or not.</returns>
        bool EvaluateCount(Predicate<int> value, out int actualCount)
        {
            int count = 0;
            for (var current = this.head.next; current != null; current = current.next)
            {
                count++;

                if (value(count))
                {
                    actualCount = count;
                    return true;
                }
            }
            actualCount = count;
            return false;
        }

        /// <summary>
        /// Get's the value indicating if the Queue is empty.
        /// </summary>
        public bool IsEmpty => this.head.next == null;

        public void Clear() => Volatile.Write(ref this.head, new Node());

        /// <summary>
        /// Get's the tail.
        /// </summary>
        /// <remarks>
        /// In order to achieve correctness we need to keep track of the tail,
        /// accessing tail.next will not do as some other thread might just moved it
        /// so in order to catch the tail we need to do a subtle form of a spin lock
        /// that will use CompareAndSet atomic instruction ( Interlocked.CompareExchange )
        /// and set ourselves to the tail if it had been moved.
        /// </remarks>
        /// <returns>Tail.</returns>
        Node GetTail()
        {
            var localTail = this.tail;
            var localNext = localTail.next;

            //if some other thread moved the tail we need to set to the right possition.
            while (localNext != null)
            {
                //set the tail.
                Interlocked.CompareExchange(ref this.tail, localNext, localTail);
                localTail = this.tail;
                localNext = localTail.next;
            }

            return this.tail;
        }

        /// <summary>
        /// Attempts to reset the Couner id.
        /// </summary>
        void TryResetCounter()
        {
            if (this.tail.id >= short.MaxValue)
            {
                int res = (this.tail.id - this.head.id);
                this.head.id = 0;
                this.tail.id = res;
            }
        }
        
        
        /// <summary>
        /// Puts a new item on the Queue.
        /// </summary>
        /// <param name="item">The value to be queued.</param>
        public bool TryEnqueue(T item)
        {
            Node localTail = null;
            var newNode = new Node();
            newNode.val = item;

            this.TryResetCounter();

            Node localTailNext = localTail.next;
            do
            {
                //get the tail.
                localTail = this.GetTail();

                //TODO: This should be atomic.
                newNode.next = localTail.next;
                newNode.id = localTail.id + 1;
                newNode.prev = localTail;
            }
            // if we arent null, then this means that some other
            // thread interffered with our plans (sic!) and we need to
            // start over.
            while (Interlocked.CompareExchange(ref localTailNext, newNode, null) != null);
            
            // if we finally are at the tail and we are the same,
            // then we switch the values to the new node, phew! :)
            Interlocked.CompareExchange(ref this.tail, newNode, localTail);

            return true;
        }
        
        /// <summary>
        /// Gets the first element in the queue.
        /// </summary>
        /// <returns>Head element.</returns>
        public bool TryDequeue(out T item)
        {
            // keep spining until we catch the propper head.
            while (true)
            {
                Node localHead = this.head;
                Node localNext = localHead.next;
                Node localTail = this.tail;

                // if the queue is empty then return the default for that
                // typeparam.
                if (localNext == null)
                {
                    item = default(T);
                    return false;
                }
                else if (localHead == localTail)
                {
                    // our tail is lagging behind so we need to swing it.
                    Interlocked.CompareExchange(ref this.tail, localHead, localTail);
                }
                else
                {
                    localNext.prev = localHead.prev;

                    // if no other thread changed the head then we are good to
                    // go and we can return the local value;
                    if (Interlocked.CompareExchange(ref this.head, localNext, localHead) == localHead)
                    {
                        item = localNext.val;
                        return true;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the last element in the queue.
        /// </summary>
        /// <returns>old tail element.</returns>
        public bool TryDequeueLast(out T item)
        {
            Node localTail;
            Node localPrev;
            var swapNode = new Node();

            do
            {
                //get the tail.
                localTail = this.GetTail();
                localPrev = localTail.prev;

                if (localPrev == null 
                    && localPrev.prev == null
                    && localPrev.prev == this.head)
                {
                    item = default(T);
                    return false;
                }


                // Set the swap node values that will exchange the element
                // in a sense that it will skip right through it.
                swapNode.next = localTail.next;
                swapNode.prev = localPrev.prev;
                swapNode.val = localPrev.val;
                swapNode.id = localPrev.id;
            }
            // In order for this to be actualy *thread safe* we need to subscribe ourselfs
            // to the same logic as the enque and create a blockade by setting the next value
            // of the tail!
            while (Interlocked.CompareExchange(ref localTail.next, localTail, null) != null);

            // do a double exchange, if we get interrupted between we should be still fine as,
            // all we need to do after the first echange is to swing the prev element to point at the
            // correct tail.
            Interlocked.CompareExchange(ref this.tail, swapNode, this.tail);
            Interlocked.CompareExchange(ref this.tail.prev.next, swapNode, this.tail.prev.next);

            item = localTail.val;
            return true;
        }

        /// <summary>
        /// Tries to peek the next value in the queue without
        /// getting it out.
        /// </summary>
        /// <param name="value">the output value.</param>
        /// <returns>the value indicating that there are still values to be peeked.</returns>
        public bool TryPeek(out T value)
        {
            Node currentNode = this.head.next;

            if (currentNode == null)
            {
                value = default(T);
                return false;
            }
            else
            {
                value = currentNode.val;
                return true;
            }
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>enumerator.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            Node currenNode = this.head.next;
            Node localTail = this.GetTail();

            while (currenNode != null)
            {
                yield return currenNode.val;

                if (currenNode == localTail)
                {
                    break;
                }
                
                currenNode = currenNode.next;
            }
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();
    }
}