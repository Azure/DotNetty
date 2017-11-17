// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System.Diagnostics.Contracts;
    using System.Threading;

    public class WorkStealingQueue<T> : IDeque<T>
        where T : class
    {
        const int InitialSize = 32;
        const int StartIndex = 0;

        volatile T[] array = new T[InitialSize];
        volatile int mask = InitialSize - 1;

        volatile int headIndex = StartIndex;
        volatile int tailIndex = StartIndex;

        readonly SpinLock foreignLock = new SpinLock(false);

        public int Count => this.tailIndex - this.headIndex;

        public bool IsEmpty => this.headIndex >= this.tailIndex;

        public bool TryEnqueue(T item)
        {
            int tail = this.tailIndex;

            // We're going to increment the tail; if we'll overflow, then we need to reset our counts
            if (tail == int.MaxValue)
            {
                bool lockTaken = false;
                try
                {
                    this.foreignLock.Enter(ref lockTaken);

                    if (this.tailIndex == int.MaxValue)
                    {
                        //
                        // Rather than resetting to zero, we'll just mask off the bits we don't care about.
                        // This way we don't need to rearrange the items already in the queue; they'll be found
                        // correctly exactly where they are.  One subtlety here is that we need to make sure that
                        // if head is currently < tail, it remains that way.  This happens to just fall out from
                        // the bit-masking, because we only do this if tail == int.MaxValue, meaning that all
                        // bits are set, so all of the bits we're keeping will also be set.  Thus it's impossible
                        // for the head to end up > than the tail, since you can't set any more bits than all of 
                        // them.
                        //
                        this.headIndex = this.headIndex & this.mask;
                        this.tailIndex = tail = this.tailIndex & this.mask;
                        Contract.Assert(this.headIndex <= this.tailIndex);
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        this.foreignLock.Exit(true);
                    }
                }
            }

            // When there are at least 2 elements' worth of space, we can take the fast path.
            if (tail < this.headIndex + this.mask)
            {
                Volatile.Write(ref this.array[tail & this.mask], item);
                this.tailIndex = tail + 1;
            }
            else
            {
                // We need to contend with foreign pops, so we lock.
                bool lockTaken = false;
                try
                {
                    this.foreignLock.Enter(ref lockTaken);

                    int head = this.headIndex;
                    int count = this.tailIndex - this.headIndex;

                    // If there is still space (one left), just add the element.
                    if (count >= this.mask)
                    {
                        // We're full; expand the queue by doubling its size.
                        var newArray = new T[this.array.Length << 1];
                        for (int i = 0; i < this.array.Length; i++)
                        {
                            newArray[i] = this.array[(i + head) & this.mask];
                        }

                        // Reset the field values, incl. the mask.
                        this.array = newArray;
                        this.headIndex = 0;
                        this.tailIndex = tail = count;
                        this.mask = (this.mask << 1) | 1;
                    }

                    Volatile.Write(ref this.array[tail & this.mask], item);
                    this.tailIndex = tail + 1;
                }
                finally
                {
                    if (lockTaken)
                    {
                        this.foreignLock.Exit(false);
                    }
                }
            }

            return true;
        }

        public bool TryPeek(out T item)
        {
            while (true)
            {
                int tail = this.tailIndex;
                if (this.headIndex >= tail)
                {
                    item = null;
                    return false;
                }
                else
                {
                    int idx = tail & this.mask;
                    item = Volatile.Read(ref this.array[idx]);

                    // Check for nulls in the array.
                    if (item == null)
                    {
                        continue;
                    }

                    return true;
                }
            }
        }

        public bool TryDequeue(out T item)
        {
            while (true)
            {
                // Decrement the tail using a fence to ensure subsequent read doesn't come before.
                int tail = this.tailIndex;
                if (this.headIndex >= tail)
                {
                    item = null;
                    return false;
                }

                tail -= 1;
                Interlocked.Exchange(ref this.tailIndex, tail);

                // If there is no interaction with a take, we can head down the fast path.
                if (this.headIndex <= tail)
                {
                    int idx = tail & this.mask;
                    item = Volatile.Read(ref this.array[idx]);

                    // Check for nulls in the array.
                    if (item == null)
                    {
                        continue;
                    }

                    this.array[idx] = null;
                    return true;
                }
                else
                {
                    // Interaction with takes: 0 or 1 elements left.
                    bool lockTaken = false;
                    try
                    {
                        this.foreignLock.Enter(ref lockTaken);

                        if (this.headIndex <= tail)
                        {
                            // Element still available. Take it.
                            int idx = tail & this.mask;
                            item = Volatile.Read(ref this.array[idx]);

                            // Check for nulls in the array.
                            if (item == null)
                            {
                                continue;
                            }

                            this.array[idx] = null;
                            return true;
                        }
                        else
                        {
                            // We lost the ----, element was stolen, restore the tail.
                            this.tailIndex = tail + 1;
                            item = null;
                            return false;
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            this.foreignLock.Exit(false);
                        }
                    }
                }
            }
        }

        public bool TryDequeueLast(out T item)
        {
            item = null;

            while (true)
            {
                if (this.headIndex >= this.tailIndex)
                {
                    return false;
                }

                bool taken = false;
                try
                {
                    this.foreignLock.TryEnter(0, ref taken);
                    if (taken)
                    {
                        // Increment head, and ensure read of tail doesn't move before it (fence).
                        int head = this.headIndex;
                        Interlocked.Exchange(ref this.headIndex, head + 1);

                        if (head < this.tailIndex)
                        {
                            int idx = head & this.mask;
                            item = Volatile.Read(ref this.array[idx]);

                            // Check for nulls in the array.
                            if (item == null)
                            {
                                continue;
                            }

                            this.array[idx] = null;
                            return true;
                        }
                        else
                        {
                            // Failed, restore head.
                            this.headIndex = head;
                            item = null;
                        }
                    }
                }
                finally
                {
                    if (taken)
                    {
                        this.foreignLock.Exit(false);
                    }
                }

                return false;
            }
        }

        public void Clear()
        {
            this.headIndex = this.tailIndex = StartIndex;
        }
    }
}