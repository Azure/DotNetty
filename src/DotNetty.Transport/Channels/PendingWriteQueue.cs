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

    /**
 * A queue of write operations which are pending for later execution. It also updates the
 * {@linkplain Channel#isWritable() writability} of the associated {@link Channel}, so that
 * the pending write operations are also considered to determine the writability.
 */

    public sealed class PendingWriteQueue
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<PendingWriteQueue>();

        readonly IChannelHandlerContext ctx;
        readonly ChannelOutboundBuffer buffer;
        readonly IMessageSizeEstimatorHandle estimatorHandle;

        // head and tail pointers for the linked-list structure. If empty head and tail are null.
        PendingWrite head;
        PendingWrite tail;
        int size;

        public PendingWriteQueue(IChannelHandlerContext ctx)
        {
            Contract.Requires(ctx != null);

            this.ctx = ctx;
            this.buffer = ctx.Channel.Unsafe.OutboundBuffer;
            this.estimatorHandle = ctx.Channel.Configuration.MessageSizeEstimator.NewHandle();
        }

        /**
     * Returns {@code true} if there are no pending write operations left in this queue.
     */

        public bool IsEmpty
        {
            get
            {
                Contract.Assert(this.ctx.Executor.InEventLoop);

                return this.head == null;
            }
        }

        /**
         * Returns the number of pending write operations.
         */

        public int Size
        {
            get
            {
                Contract.Assert(this.ctx.Executor.InEventLoop);

                return this.size;
            }
        }

        /**
         * Add the given {@code msg} and {@link ChannelPromise}.
         */

        public Task Add(object msg)
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);
            Contract.Requires(msg != null);

            int messageSize = this.estimatorHandle.Size(msg);
            if (messageSize < 0)
            {
                // Size may be unknow so just use 0
                messageSize = 0;
            }
            var promise = new TaskCompletionSource();
            PendingWrite write = PendingWrite.NewInstance(msg, messageSize, promise);
            PendingWrite currentTail = this.tail;
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
            if (this.buffer != null)
            {
                this.buffer.IncrementPendingOutboundBytes(write.Size);
            }
            return promise.Task;
        }

        /**
         * Remove all pending write operation and fail them with the given {@link Throwable}. The message will be released
         * via {@link ReferenceCountUtil#safeRelease(Object)}.
         */

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
                ReferenceCountUtil.SafeRelease(write.Msg);
                TaskCompletionSource promise = write.Promise;
                this.Recycle(write, false);
                SafeFail(promise, cause);
                write = next;
            }
            this.AssertEmpty();
        }

        /**
         * Remove a pending write operation and fail it with the given {@link Throwable}. The message will be released via
         * {@link ReferenceCountUtil#safeRelease(Object)}.
         */

        public void RemoveAndFail(Exception cause)
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);
            Contract.Requires(cause != null);

            PendingWrite write = this.head;

            if (write == null)
            {
                return;
            }
            ReferenceCountUtil.SafeRelease(write.Msg);
            TaskCompletionSource promise = write.Promise;
            SafeFail(promise, cause);
            this.Recycle(write, true);
        }

        /**
         * Remove all pending write operation and performs them via
         * {@link ChannelHandlerContext#write(Object, ChannelPromise)}.
         *
         * @return  {@link ChannelFuture} if something was written and {@code null}
         *          if the {@link PendingWriteQueue} is empty.
         */

        public Task RemoveAndWriteAll()
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);

            if (this.size == 1)
            {
                // No need to use ChannelPromiseAggregator for this case.
                return this.RemoveAndWrite();
            }
            PendingWrite write = this.head;
            if (write == null)
            {
                // empty so just return null
                return null;
            }

            // Guard against re-entrance by directly reset
            this.head = this.tail = null;
            int currentSize = this.size;
            this.size = 0;

            var tasks = new List<Task>(currentSize);
            while (write != null)
            {
                PendingWrite next = write.Next;
                object msg = write.Msg;
                TaskCompletionSource promise = write.Promise;
                this.Recycle(write, false);
                this.ctx.WriteAsync(msg).LinkOutcome(promise);
                tasks.Add(promise.Task);
                write = next;
            }
            this.AssertEmpty();
            return Task.WhenAll(tasks);
        }

        void AssertEmpty()
        {
            Contract.Assert(this.tail == null && this.head == null && this.size == 0);
        }

        /**
         * Removes a pending write operation and performs it via
         * {@link ChannelHandlerContext#write(Object, ChannelPromise)}.
         *
         * @return  {@link ChannelFuture} if something was written and {@code null}
         *          if the {@link PendingWriteQueue} is empty.
         */

        public Task RemoveAndWrite()
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);

            PendingWrite write = this.head;
            if (write == null)
            {
                return null;
            }
            object msg = write.Msg;
            TaskCompletionSource promise = write.Promise;
            this.Recycle(write, true);
            this.ctx.WriteAsync(msg).LinkOutcome(promise);
            return promise.Task;
        }

        /// <summary>
        ///     Removes a pending write operation and release it's message via {@link ReferenceCountUtil#safeRelease(Object)}.
        /// </summary>
        /// <returns><seealso cref="TaskCompletionSource" /> of the pending write or <c>null</c> if the queue is empty.</returns>
        public TaskCompletionSource Remove()
        {
            Contract.Assert(this.ctx.Executor.InEventLoop);

            PendingWrite write = this.head;
            if (write == null)
            {
                return null;
            }
            TaskCompletionSource promise = write.Promise;
            ReferenceCountUtil.SafeRelease(write.Msg);
            this.Recycle(write, true);
            return promise;
        }

        /// <summary>
        ///     Return the current message or {@code null} if empty.
        /// </summary>
        public object Current
        {
            get
            {
                Contract.Assert(this.ctx.Executor.InEventLoop);

                PendingWrite write = this.head;
                if (write == null)
                {
                    return null;
                }
                return write.Msg;
            }
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

            write.Recycle();
            // We need to guard against null as channel.unsafe().outboundBuffer() may returned null
            // if the channel was already closed when constructing the PendingWriteQueue.
            // See https://github.com/netty/netty/issues/3967
            if (this.buffer != null)
            {
                this.buffer.DecrementPendingOutboundBytes(writeSize);
            }
        }

        static void SafeFail(TaskCompletionSource promise, Exception cause)
        {
            if ( /*!(promise instanceof VoidChannelPromise) && */!promise.TrySetException(cause))
            {
                Logger.Warn("Failed to mark a promise as failure because it's done already: {}", promise, cause);
            }
        }

        /**
         * Holds all meta-data and construct the linked-list structure.
         */

        sealed class PendingWrite
        {
            static readonly ThreadLocalPool<PendingWrite> Pool = new ThreadLocalPool<PendingWrite>(handle => new PendingWrite(handle));

            readonly ThreadLocalPool.Handle handle;
            public PendingWrite Next;
            public long Size;
            public TaskCompletionSource Promise;
            public object Msg;

            PendingWrite(ThreadLocalPool.Handle handle)
            {
                this.handle = handle;
            }

            public static PendingWrite NewInstance(object msg, int size, TaskCompletionSource promise)
            {
                PendingWrite write = Pool.Take();
                write.Size = size;
                write.Msg = msg;
                write.Promise = promise;
                return write;
            }

            public void Recycle()
            {
                this.Size = 0;
                this.Next = null;
                this.Msg = null;
                this.Promise = null;
                this.handle.Release(this);
            }
        }
    }
}