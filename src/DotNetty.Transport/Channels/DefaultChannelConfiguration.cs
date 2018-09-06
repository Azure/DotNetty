// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels.Sockets;

    /// <summary>
    ///     Shared configuration for SocketAsyncChannel. Provides access to pre-configured resources like ByteBuf allocator and
    ///     IO buffer pools
    /// </summary>
    public class DefaultChannelConfiguration : IChannelConfiguration
    {
        static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(30);

        volatile IByteBufferAllocator allocator = ByteBufferUtil.DefaultAllocator;
        volatile IRecvByteBufAllocator recvByteBufAllocator = FixedRecvByteBufAllocator.Default;
        volatile IMessageSizeEstimator messageSizeEstimator = DefaultMessageSizeEstimator.Default;

        volatile int autoRead = 1;
        volatile int writeSpinCount = 16;
        volatile int writeBufferHighWaterMark = 64 * 1024;
        volatile int writeBufferLowWaterMark = 32 * 1024;
        long connectTimeout = DefaultConnectTimeout.Ticks;

        protected readonly IChannel Channel;

        public DefaultChannelConfiguration(IChannel channel)
            : this(channel, new AdaptiveRecvByteBufAllocator())
        {
        }

        public DefaultChannelConfiguration(IChannel channel, IRecvByteBufAllocator allocator)
        {
            Contract.Requires(channel != null);

            this.Channel = channel;
            var maxMessagesAllocator = allocator as IMaxMessagesRecvByteBufAllocator;
            if (maxMessagesAllocator != null)
            {
                maxMessagesAllocator.MaxMessagesPerRead = channel.Metadata.DefaultMaxMessagesPerRead;
            }
            else if (allocator == null)
            {
                throw new ArgumentNullException(nameof(allocator));
            }
            this.RecvByteBufAllocator = allocator;
        }

        public virtual T GetOption<T>(ChannelOption<T> option)
        {
            Contract.Requires(option != null);

            if (ChannelOption.ConnectTimeout.Equals(option))
            {
                return (T)(object)this.ConnectTimeout; // no boxing will happen, compiler optimizes away such casts
            }
            if (ChannelOption.WriteSpinCount.Equals(option))
            {
                return (T)(object)this.WriteSpinCount;
            }
            if (ChannelOption.Allocator.Equals(option))
            {
                return (T)this.Allocator;
            }
            if (ChannelOption.RcvbufAllocator.Equals(option))
            {
                return (T)this.RecvByteBufAllocator;
            }
            if (ChannelOption.AutoRead.Equals(option))
            {
                return (T)(object)this.AutoRead;
            }
            if (ChannelOption.WriteBufferHighWaterMark.Equals(option))
            {
                return (T)(object)this.WriteBufferHighWaterMark;
            }
            if (ChannelOption.WriteBufferLowWaterMark.Equals(option))
            {
                return (T)(object)this.WriteBufferLowWaterMark;
            }
            if (ChannelOption.MessageSizeEstimator.Equals(option))
            {
                return (T)this.MessageSizeEstimator;
            }
            return default(T);
        }

        public bool SetOption(ChannelOption option, object value) => option.Set(this, value);

        public virtual bool SetOption<T>(ChannelOption<T> option, T value)
        {
            this.Validate(option, value);

            if (ChannelOption.ConnectTimeout.Equals(option))
            {
                this.ConnectTimeout = (TimeSpan)(object)value;
            }
            else if (ChannelOption.WriteSpinCount.Equals(option))
            {
                this.WriteSpinCount = (int)(object)value;
            }
            else if (ChannelOption.Allocator.Equals(option))
            {
                this.Allocator = (IByteBufferAllocator)value;
            }
            else if (ChannelOption.RcvbufAllocator.Equals(option))
            {
                this.RecvByteBufAllocator = (IRecvByteBufAllocator)value;
            }
            else if (ChannelOption.AutoRead.Equals(option))
            {
                this.AutoRead = (bool)(object)value;
            }
            else if (ChannelOption.WriteBufferHighWaterMark.Equals(option))
            {
                this.WriteBufferHighWaterMark = (int)(object)value;
            }
            else if (ChannelOption.WriteBufferLowWaterMark.Equals(option))
            {
                this.WriteBufferLowWaterMark = (int)(object)value;
            }
            else if (ChannelOption.MessageSizeEstimator.Equals(option))
            {
                this.MessageSizeEstimator = (IMessageSizeEstimator)value;
            }
            else
            {
                return false;
            }

            return true;
        }

        protected virtual void Validate<T>(ChannelOption<T> option, T value)
        {
            Contract.Requires(option != null);
            option.Validate(value);
        }

        public TimeSpan ConnectTimeout
        {
            get { return new TimeSpan(Volatile.Read(ref this.connectTimeout)); }
            set
            {
                Contract.Requires(value >= TimeSpan.Zero);
                Volatile.Write(ref this.connectTimeout, value.Ticks);
            }
        }

        public IByteBufferAllocator Allocator
        {
            get { return this.allocator; }
            set
            {
                Contract.Requires(value != null);
                this.allocator = value;
            }
        }

        public IRecvByteBufAllocator RecvByteBufAllocator
        {
            get { return this.recvByteBufAllocator; }
            set
            {
                Contract.Requires(value != null);
                this.recvByteBufAllocator = value;
            }
        }

        public IMessageSizeEstimator MessageSizeEstimator
        {
            get { return this.messageSizeEstimator; }
            set
            {
                Contract.Requires(value != null);
                this.messageSizeEstimator = value;
            }
        }

        public bool AutoRead
        {
            get { return this.autoRead == 1; }
            set
            {
#pragma warning disable 420 // atomic exchange is ok
                bool oldAutoRead = Interlocked.Exchange(ref this.autoRead, value ? 1 : 0) == 1;
#pragma warning restore 420
                if (value && !oldAutoRead)
                {
                    this.Channel.Read();
                }
                else if (!value && oldAutoRead)
                {
                    this.AutoReadCleared();
                }
            }
        }

        protected virtual void AutoReadCleared()
        {
        }

        public int WriteBufferHighWaterMark
        {
            get { return this.writeBufferHighWaterMark; }
            set
            {
                Contract.Requires(value >= 0);
                Contract.Requires(value >= this.writeBufferLowWaterMark);

                this.writeBufferHighWaterMark = value;
            }
        }

        public int WriteBufferLowWaterMark
        {
            get { return this.writeBufferLowWaterMark; }
            set
            {
                Contract.Requires(value >= 0);
                Contract.Requires(value <= this.writeBufferHighWaterMark);

                this.writeBufferLowWaterMark = value;
            }
        }

        public int WriteSpinCount
        {
            get { return this.writeSpinCount; }
            set
            {
                Contract.Requires(value >= 1);

                this.writeSpinCount = value;
            }
        }
    }
}