// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Threading;
    using DotNetty.Common.Utilities;

    /// Forked from
    /// <a href="https://github.com/JCTools/JCTools">JCTools</a>
    /// .
    /// A concurrent access enabling class used by circular array based queues this class exposes an offset computation
    /// method along with differently memory fenced load/store methods into the underlying array. The class is pre-padded and
    /// the array is padded on either side to help with False sharing prvention. It is expected theat subclasses handle post
    /// padding.
    /// <p />
    /// Offset calculation is separate from access to enable the reuse of a give compute offset.
    /// <p />
    /// Load/Store methods using a
    /// <i>buffer</i>
    /// parameter are provided to allow the prevention of field reload after a
    /// LoadLoad barrier.
    /// <p />
    /// @param
    /// <E>
    abstract class ConcurrentCircularArrayQueue<T> : ConcurrentCircularArrayQueueL0Pad<T>
        where T : class
    {
        protected static readonly int RefBufferPad = (64 * 2) / IntPtr.Size;
        protected long Mask;
        protected readonly T[] Buffer;

        protected ConcurrentCircularArrayQueue(int capacity)
        {
            int actualCapacity = IntegerExtensions.RoundUpToPowerOfTwo(capacity);
            this.Mask = actualCapacity - 1;
            // pad data on either end with some empty slots.
            this.Buffer = new T[actualCapacity + RefBufferPad * 2];
        }

        /// @param index desirable element index
        /// @return the offset in bytes within the array for a given index.
        protected long CalcElementOffset(long index)
        {
            return CalcElementOffset(index, this.Mask);
        }

        /// @param index desirable element index
        /// @param mask
        /// @return the offset in bytes within the array for a given index.
        protected static long CalcElementOffset(long index, long mask)
        {
            return RefBufferPad + (index & mask);
        }

        /// A plain store (no ordering/fences) of an element to a given offset
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @param e a kitty
        protected void SpElement(long offset, T e)
        {
            SpElement(this.Buffer, offset, e);
        }

        /// A plain store (no ordering/fences) of an element to a given offset
        /// @param buffer this.buffer
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @param e an orderly kitty
        protected static void SpElement(T[] buffer, long offset, T e)
        {
            buffer[offset] = e;
        }

        /// An ordered store(store + StoreStore barrier) of an element to a given offset
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @param e an orderly kitty
        protected void SoElement(long offset, T e)
        {
            SoElement(this.Buffer, offset, e);
        }

        /// An ordered store(store + StoreStore barrier) of an element to a given offset
        /// @param buffer this.buffer
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @param e an orderly kitty
        protected static void SoElement(T[] buffer, long offset, T e)
        {
            Volatile.Write(ref buffer[offset], e);
        }

        /// A plain load (no ordering/fences) of an element from a given offset.
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @return the element at the offset
        protected T LpElement(long offset)
        {
            return LpElement(this.Buffer, offset);
        }

        /// A plain load (no ordering/fences) of an element from a given offset.
        /// @param buffer this.buffer
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @return the element at the offset
        protected static T LpElement(T[] buffer, long offset)
        {
            return buffer[offset];
        }

        /// A volatile load (load + LoadLoad barrier) of an element from a given offset.
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @return the element at the offset
        protected T LvElement(long offset)
        {
            return LvElement(this.Buffer, offset);
        }

        /// A volatile load (load + LoadLoad barrier) of an element from a given offset.
        /// @param buffer this.buffer
        /// @param offset computed via {@link ConcurrentCircularArrayQueue#calcElementOffset(long)}
        /// @return the element at the offset
        protected static T LvElement(T[] buffer, long offset)
        {
            return Volatile.Read(ref buffer[offset]);
        }

        public override void Clear()
        {
            while (this.Dequeue() != null || !this.IsEmpty)
            {
                // looping
            }
        }

        public int Capacity()
        {
            return (int)(this.Mask + 1);
        }
    }

    abstract class ConcurrentCircularArrayQueueL0Pad<T> : AbstractQueue<T>
    {
#pragma warning disable 169 // padded reference
        long p00, p01, p02, p03, p04, p05, p06, p07;
        long p30, p31, p32, p33, p34, p35, p36, p37;
#pragma warning restore 169
    }
}