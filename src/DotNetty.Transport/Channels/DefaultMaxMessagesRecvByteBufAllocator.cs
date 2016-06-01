// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;

    /// <summary>
    ///     Default implementation of <see cref="IMaxMessagesRecvByteBufAllocator" /> which respects
    ///     <see cref="IChannelConfiguration.AutoRead" />
    ///     and also prevents overflow.
    /// </summary>
    public abstract class DefaultMaxMessagesRecvByteBufAllocator : IMaxMessagesRecvByteBufAllocator
    {
        volatile int maxMessagesPerRead;

        protected DefaultMaxMessagesRecvByteBufAllocator()
            : this(1)
        {
        }

        protected DefaultMaxMessagesRecvByteBufAllocator(int maxMessagesPerRead)
        {
            this.MaxMessagesPerRead = maxMessagesPerRead;
        }

        public int MaxMessagesPerRead
        {
            get { return this.maxMessagesPerRead; }
            set
            {
                Contract.Requires(value > 0);
                this.maxMessagesPerRead = value;
            }
        }

        public abstract IRecvByteBufAllocatorHandle NewHandle();

        /// <summary>Focuses on enforcing the maximum messages per read condition for <see cref="ContinueReading" />.</summary>
        protected abstract class MaxMessageHandle<T> : IRecvByteBufAllocatorHandle
            where T : IMaxMessagesRecvByteBufAllocator
        {
            protected readonly T Owner;
            IChannelConfiguration config;
            int maxMessagePerRead;
            int totalMessages;
            int totalBytesRead;
            int lastBytesRead;

            protected MaxMessageHandle(T owner)
            {
                this.Owner = owner;
            }

            public abstract int Guess();

            /// <summary>Only <see cref="IChannelConfiguration.MaxMessagesPerRead" /> is used.</summary>
            public void Reset(IChannelConfiguration config)
            {
                this.config = config;
                this.maxMessagePerRead = this.Owner.MaxMessagesPerRead;
                this.totalMessages = this.totalBytesRead = 0;
            }

            public IByteBuffer Allocate(IByteBufferAllocator alloc) => alloc.Buffer(this.Guess());

            public void IncMessagesRead(int amt) => this.totalMessages += amt;

            public int LastBytesRead
            {
                get { return this.lastBytesRead; }
                set
                {
                    this.lastBytesRead = value;
                    // Ignore if bytes is negative, the interface contract states it will be detected externally after call.
                    // The value may be "invalid" after this point, but it doesn't matter because reading will be stopped.
                    this.totalBytesRead += value;
                    if (this.totalBytesRead < 0)
                    {
                        this.totalBytesRead = int.MaxValue;
                    }
                }
            }

            public virtual bool ContinueReading()
            {
                return this.config.AutoRead
                    && this.AttemptedBytesRead == this.lastBytesRead
                    && this.totalMessages < this.maxMessagePerRead
                    && this.totalBytesRead < int.MaxValue;
            }

            public virtual void ReadComplete()
            {
            }

            public virtual int AttemptedBytesRead { get; set; }

            protected int TotalBytesRead() => this.totalBytesRead;
        }
    }
}