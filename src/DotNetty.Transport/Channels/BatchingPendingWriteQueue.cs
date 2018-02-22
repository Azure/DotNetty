// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     A queue of write operations which are pending for later execution. It also updates the
    ///     <see cref="IChannel.IsWritable">writability</see> of the associated <see cref="IChannel" />, so that
    ///     the pending write operations are also considered to determine the writability.
    /// </summary>
    public sealed class BatchingPendingWriteQueue
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<PendingWriteQueue>();

        readonly IChannelHandlerContext ctx;
        readonly int maxSize;
        readonly ChannelOutboundBuffer buffer;
        readonly IMessageSizeEstimatorHandle estimatorHandle;

        // head and tail pointers for the linked-list structure. If empty head and tail are null.
        PendingWrite head;
        PendingWrite tail;
        int size;

        public BatchingPendingWriteQueue(IChannelHandlerContext ctx, int maxSize)
        {
            Contract.Requires(ctx != null);

            this.ctx = ctx;
            this.maxSize = maxSize;
            this.buffer = ctx.Channel.Unsafe.OutboundBuffer;
            this.estimatorHandle = ctx.Channel.Configuration.MessageSizeEstimator.NewHandle();
        }

        /// <summary>Returns <c>true</c> if there are no pending write operations left in this queue.</summary>
        public bool IsEmpty
        {
            get
            {
                Contract.Assert(this.ctx.Executor.InEventLoop);

                return this.head == null;
            }
        }

        /// <summary>Returns the number of pending write operations.</summary>
        public int Size
        {
            get
            {
                Contract.Assert(this.ctx.Executor.InEventLoop);

                return this.size;
            }
        }

        /// <summary>Add the given <c>msg</c> and returns <see cref="Task" /> for completion of processing <c>msg</c>.</summary>
        public ChannelFuture Add(object msg)
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);
            Contract.Requires(msg != null);

            int messageSize = this.estimatorHandle.Size(msg);
            if (messageSize < 0)
            {
                // Size may be unknow so just use 0
                messageSize = 0;
            }
            PendingWrite currentTail = this.tail;
            if (currentTail != null)
            {
                bool canBundle = this.CanBatch(msg, messageSize, currentTail.Size);
                if (canBundle)
                {
                    currentTail.Add(msg, messageSize);
                    return currentTail;
                }
            }

            PendingWrite write = PendingWrite.NewInstance(this.ctx.Executor, msg, messageSize);
            if (currentTail == null)
            {
                this.tail = this.head = write;
            }
            else
            {
                currentTail.Next = write;
                this.tail = write;
            }
            this.size++;
            // We need to guard against null as channel.Unsafe.OutboundBuffer may returned null
            // if the channel was already closed when constructing the PendingWriteQueue.
            // See https://github.com/netty/netty/issues/3967
            this.buffer?.IncrementPendingOutboundBytes(messageSize);
            return write;
        }

        /// <summary>
        ///     Remove all pending write operation and fail them with the given <see cref="Exception" />. The messages will be
        ///     released
        ///     via <see cref="ReferenceCountUtil.SafeRelease(object)" />.
        /// </summary>
        public void RemoveAndFailAll(Exception cause)
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);
            Contract.Requires(cause != null);

            // Guard against re-entrance by directly reset
            PendingWrite write = this.head;
            this.head = this.tail = null;
            this.size = 0;

            while (write != null)
            {
                PendingWrite next = write.Next;
                ReleaseMessages(write.Messages);
                Util.SafeSetFailure(write, cause, Logger);
                this.Recycle(write, false);
                
                write = next;
            }
            this.AssertEmpty();
        }

        /// <summary>
        ///     Remove a pending write operation and fail it with the given <see cref="Exception" />. The message will be released
        ///     via
        ///     <see cref="ReferenceCountUtil.SafeRelease(object)" />.
        /// </summary>
        public void RemoveAndFail(Exception cause)
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);
            Contract.Requires(cause != null);

            PendingWrite write = this.head;

            if (write == null)
            {
                return;
            }
            ReleaseMessages(write.Messages);
            Util.SafeSetFailure(write, cause, Logger);
            this.Recycle(write, true);
        }

        /// <summary>
        ///     Remove all pending write operation and performs them via
        ///     <see cref="IChannelHandlerContext.WriteAsync(object)" />.
        /// </summary>
        /// <returns>
        ///     <see cref="Task" /> if something was written and <c>null</c> if the <see cref="BatchingPendingWriteQueue" />
        ///     is empty.
        /// </returns>
        public ChannelFuture RemoveAndWriteAllAsync()
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);

            if (this.size == 1)
            {
                // No need to use ChannelPromiseAggregator for this case.
                return this.RemoveAndWriteAsync();
            }
            PendingWrite write = this.head;
            if (write == null)
            {
                // empty so just return null
                return ChannelFuture.Completed;
            }

            // Guard against re-entrance by directly reset
            this.head = this.tail = null;
            int currentSize = this.size;
            this.size = 0;

            var tasks = new List<ChannelFuture>(currentSize);
            while (write != null)
            {
                PendingWrite next = write.Next;
                object msg = write.Messages;
                this.Recycle(write, false);
                this.ctx.WriteAsync(msg).LinkOutcome(write);
                
                tasks.Add(write);
                write = next;
            }
            this.AssertEmpty();
            return new ChannelFuture(new AggregatingPromise(tasks));
        }

        void AssertEmpty() => Contract.Assert(this.tail == null && this.head == null && this.size == 0);

        /// <summary>
        ///     Removes a pending write operation and performs it via
        ///     <see cref="IChannelHandlerContext.WriteAsync(object)"/>.
        /// </summary>
        /// <returns>
        ///     <see cref="Task" /> if something was written and <c>null</c> if the <see cref="BatchingPendingWriteQueue" />
        ///     is empty.
        /// </returns>
        public ChannelFuture RemoveAndWriteAsync()
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);

            PendingWrite write = this.head;
            if (write == null)
            {
                return ChannelFuture.Completed;
            }
            object msg = write.Messages;
            this.Recycle(write, true);
            this.ctx.WriteAsync(msg).LinkOutcome(write);
            return new ChannelFuture(write);
        }

        /// <summary>
        ///     Removes a pending write operation and release it's message via <see cref="ReferenceCountUtil.SafeRelease(object)"/>.
        /// </summary>
        /// <returns><see cref="TaskCompletionSource" /> of the pending write or <c>null</c> if the queue is empty.</returns>
        public IChannelPromise Remove()
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);

            PendingWrite write = this.head;
            if (write == null)
            {
                return null;
            }
            //TaskCompletionSource promise = write.Promise;
            ReferenceCountUtil.SafeRelease(write.Messages);
            this.Recycle(write, true);
            return write;
        }

        /// <summary>
        ///     Return the current message or <c>null</c> if empty.
        /// </summary>
        public List<object> Current
        {
            get
            {
                Contract.Assert(this.ctx.Executor.InEventLoop);

                return this.head?.Messages;
            }
        }

        public long? CurrentSize
        {
            get
            {
                Contract.Assert(this.ctx.Executor.InEventLoop);

                return this.head?.Size;
            }
        }

        bool CanBatch(object message, int size, long currentBatchSize)
        {
            if (size < 0)
            {
                return false;
            }

            if (currentBatchSize + size > this.maxSize)
            {
                return false;
            }

            return true;
        }

        void Recycle(PendingWrite write, bool update)
        {
            PendingWrite next = write.Next;
            long writeSize = write.Size;

            if (update)
            {
                if (next == null)
                {
                    // Handled last PendingWrite so rest head and tail
                    // Guard against re-entrance by directly reset
                    this.head = this.tail = null;
                    this.size = 0;
                }
                else
                {
                    this.head = next;
                    this.size--;
                    Contract.Assert(this.size > 0);
                }
            }

            //write.Recycle();
            
            // We need to guard against null as channel.unsafe().outboundBuffer() may returned null
            // if the channel was already closed when constructing the PendingWriteQueue.
            // See https://github.com/netty/netty/issues/3967
            this.buffer?.DecrementPendingOutboundBytes(writeSize);
        }

        static void ReleaseMessages(List<object> messages)
        {
            foreach (object msg in messages)
            {
                ReferenceCountUtil.SafeRelease(msg);
            }
        }

        /// <summary>Holds all meta-data and construct the linked-list structure.</summary>
        sealed class PendingWrite : AbstractRecyclableChannelPromise
        {
            static readonly ThreadLocalPool<PendingWrite> Pool = new ThreadLocalPool<PendingWrite>(handle => new PendingWrite(handle));

            public PendingWrite Next;
            public long Size;
            public readonly List<object> Messages;

            PendingWrite(ThreadLocalPool.Handle handle) : base(handle)
            {
                this.Messages = new List<object>();
            }

            public static PendingWrite NewInstance(IEventExecutor executor, object msg, int size)
            {
                PendingWrite write = Pool.Take();
                write.Init(executor);
                write.Add(msg, size);
                return write;
            }

            public void Add(object msg, int size)
            {
                this.Messages.Add(msg);
                this.Size += size;
            }

            protected override void Recycle()
            {
                this.Size = 0;
                this.Next = null;
                this.Messages.Clear();
                base.Recycle();
                //this.handle.Release(this);
            }
        }
    }
}