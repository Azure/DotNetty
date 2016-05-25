// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     The <see cref="IRecvByteBufAllocator" /> that automatically increases and
    ///     decreases the predicted buffer size on feed back.
    ///     <p />
    ///     It gradually increases the expected number of readable bytes if the previous
    ///     read fully filled the allocated buffer. It gradually decreases the expected
    ///     number of readable bytes if the read operation was not able to fill a certain
    ///     amount of the allocated buffer two times consecutively. Otherwise, it keeps
    ///     returning the same prediction.
    /// </summary>
    public class AdaptiveRecvByteBufAllocator : DefaultMaxMessagesRecvByteBufAllocator
    {
        const int DefaultMinimum = 64;
        const int DefaultInitial = 1024;
        const int DefaultMaximum = 65536;

        const int IndexIncrement = 4;
        const int IndexDecrement = 1;

        static readonly int[] SizeTable;

        static AdaptiveRecvByteBufAllocator()
        {
            var sizeTable = new List<int>();
            for (int i = 16; i < 512; i += 16)
            {
                sizeTable.Add(i);
            }

            for (int i = 512; i > 0; i <<= 1)
            {
                sizeTable.Add(i);
            }

            SizeTable = sizeTable.ToArray();
        }

        static int GetSizeTableIndex(int size)
        {
            for (int low = 0, high = SizeTable.Length - 1;;)
            {
                if (high < low)
                {
                    return low;
                }
                if (high == low)
                {
                    return high;
                }

                int mid = (low + high).RightUShift(1);
                int a = SizeTable[mid];
                int b = SizeTable[mid + 1];
                if (size > b)
                {
                    low = mid + 1;
                }
                else if (size < a)
                {
                    high = mid - 1;
                }
                else if (size == a)
                {
                    return mid;
                }
                else
                {
                    return mid + 1;
                }
            }
        }

        sealed class HandleImpl : MaxMessageHandle<AdaptiveRecvByteBufAllocator>
        {
            readonly int minIndex;
            readonly int maxIndex;
            int index;
            int nextReceiveBufferSize;
            bool decreaseNow;

            public HandleImpl(AdaptiveRecvByteBufAllocator owner, int minIndex, int maxIndex, int initial)
                : base(owner)
            {
                this.minIndex = minIndex;
                this.maxIndex = maxIndex;

                this.index = GetSizeTableIndex(initial);
                this.nextReceiveBufferSize = SizeTable[this.index];
            }

            public override int Guess() => this.nextReceiveBufferSize;

            void Record(int actualReadBytes)
            {
                if (actualReadBytes <= SizeTable[Math.Max(0, this.index - IndexDecrement - 1)])
                {
                    if (this.decreaseNow)
                    {
                        this.index = Math.Max(this.index - IndexDecrement, this.minIndex);
                        this.nextReceiveBufferSize = SizeTable[this.index];
                        this.decreaseNow = false;
                    }
                    else
                    {
                        this.decreaseNow = true;
                    }
                }
                else if (actualReadBytes >= this.nextReceiveBufferSize)
                {
                    this.index = Math.Min(this.index + IndexIncrement, this.maxIndex);
                    this.nextReceiveBufferSize = SizeTable[this.index];
                    this.decreaseNow = false;
                }
            }

            public override void ReadComplete() => this.Record(this.TotalBytesRead());
        }

        readonly int minIndex;
        readonly int maxIndex;
        readonly int initial;

        /// <summary>
        ///     Creates a new predictor with the default parameters.  With the default
        ///     parameters, the expected buffer size starts from <c>1024</c>, does not
        ///     go down below <c>64</c>, and does not go up above <c>65536</c>.
        /// </summary>
        public AdaptiveRecvByteBufAllocator()
            : this(DefaultMinimum, DefaultInitial, DefaultMaximum)
        {
        }

        /// <summary>Creates a new predictor with the specified parameters.</summary>
        /// <param name="minimum">the inclusive lower bound of the expected buffer size</param>
        /// <param name="initial">the initial buffer size when no feed back was received</param>
        /// <param name="maximum">the inclusive upper bound of the expected buffer size</param>
        public AdaptiveRecvByteBufAllocator(int minimum, int initial, int maximum)
        {
            Contract.Requires(minimum > 0);
            Contract.Requires(initial >= minimum);
            Contract.Requires(maximum >= initial);

            int min = GetSizeTableIndex(minimum);
            if (SizeTable[min] < minimum)
            {
                this.minIndex = min + 1;
            }
            else
            {
                this.minIndex = min;
            }

            int max = GetSizeTableIndex(maximum);
            if (SizeTable[max] > maximum)
            {
                this.maxIndex = max - 1;
            }
            else
            {
                this.maxIndex = max;
            }

            this.initial = initial;
        }

        public override IRecvByteBufAllocatorHandle NewHandle() => new HandleImpl(this, this.minIndex, this.maxIndex, this.initial);
    }
}