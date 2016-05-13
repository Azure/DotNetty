// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public sealed class ChannelOutboundBuffer
    {
#pragma warning disable 420 // all volatile fields are used with referenced in Interlocked methods only

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ChannelOutboundBuffer>();

        static readonly ThreadLocalByteBufferList NioBuffers = new ThreadLocalByteBufferList();

        readonly IChannel channel;

        // Entry(flushedEntry) --> ... Entry(unflushedEntry) --> ... Entry(tailEntry)
        //
        // The Entry that is the first in the linked-list structure that was flushed
        Entry flushedEntry;
        // The Entry which is the first unflushed in the linked-list structure
        Entry unflushedEntry;
        // The Entry which represents the tail of the buffer
        Entry tailEntry;
        // The number of flushed entries that are not written yet
        int flushed;

        //int nioBufferCount;

        bool inFail;

        long totalPendingSize;

        volatile int unwritable;

        internal ChannelOutboundBuffer(IChannel channel)
        {
            this.channel = channel;
        }

        /// <summary>
        ///     Add given message to this {@link ChannelOutboundBuffer}. The given {@link ChannelPromise} will be notified once
        ///     the message was written.
        /// </summary>
        public void AddMessage(object msg, int size, TaskCompletionSource promise)
        {
            Entry entry = Entry.NewInstance(msg, size, promise);
            if (this.tailEntry == null)
            {
                this.flushedEntry = null;
                this.tailEntry = entry;
            }
            else
            {
                Entry tail = this.tailEntry;
                tail.Next = entry;
                this.tailEntry = entry;
            }
            if (this.unflushedEntry == null)
            {
                this.unflushedEntry = entry;
            }

            // increment pending bytes after adding message to the unflushed arrays.
            // See https://github.com/netty/netty/issues/1619
            this.IncrementPendingOutboundBytes(size, false);
        }

        /// <summary>
        ///     Add a flush to this {@link ChannelOutboundBuffer}. This means all previous added messages are marked as flushed
        ///     and so you will be able to handle them.
        /// </summary>
        public void AddFlush()
        {
            // There is no need to process all entries if there was already a flush before and no new messages
            // where added in the meantime.
            //
            // See https://github.com/netty/netty/issues/2577
            Entry entry = this.unflushedEntry;
            if (entry != null)
            {
                if (this.flushedEntry == null)
                {
                    // there is no flushedEntry yet, so start with the entry
                    this.flushedEntry = entry;
                }
                do
                {
                    this.flushed++;
                    if (!entry.Promise.SetUncancellable())
                    {
                        // Was cancelled so make sure we free up memory and notify about the freed bytes
                        int pending = entry.Cancel();
                        this.DecrementPendingOutboundBytes(pending, false, true);
                    }
                    entry = entry.Next;
                }
                while (entry != null);

                // All flushed so reset unflushedEntry
                this.unflushedEntry = null;
            }
        }

        /// <summary>
        ///     Increment the pending bytes which will be written at some point.
        ///     This method is thread-safe!
        /// </summary>
        internal void IncrementPendingOutboundBytes(long size) => this.IncrementPendingOutboundBytes(size, true);

        void IncrementPendingOutboundBytes(long size, bool invokeLater)
        {
            if (size == 0)
            {
                return;
            }

            long newWriteBufferSize = Interlocked.Add(ref this.totalPendingSize, size);
            if (newWriteBufferSize >= this.channel.Configuration.WriteBufferHighWaterMark)
            {
                this.SetUnwritable(invokeLater);
            }
        }

        /// <summary>
        ///     Decrement the pending bytes which will be written at some point.
        ///     This method is thread-safe!
        /// </summary>
        internal void DecrementPendingOutboundBytes(long size) => this.DecrementPendingOutboundBytes(size, true, true);

        void DecrementPendingOutboundBytes(long size, bool invokeLater, bool notifyWritability)
        {
            if (size == 0)
            {
                return;
            }

            long newWriteBufferSize = Interlocked.Add(ref this.totalPendingSize, -size);
            if (notifyWritability && (newWriteBufferSize == 0
                || newWriteBufferSize <= this.channel.Configuration.WriteBufferLowWaterMark))
            {
                this.SetWritable(invokeLater);
            }
        }

        /// <summary>
        ///     Return the current message to write or {@code null} if nothing was flushed before and so is ready to be written.
        /// </summary>
        public object Current => this.flushedEntry?.Message;

        /// <summary>
        ///     Notify the {@link ChannelPromise} of the current message about writing progress.
        /// </summary>
        public void Progress(long amount)
        {
            // todo: support progress report?
            //Entry e = this.flushedEntry;
            //Contract.Assert(e != null);
            //var p = e.promise;
            //if (p is ChannelProgressivePromise)
            //{
            //    long progress = e.progress + amount;
            //    e.progress = progress;
            //    ((ChannelProgressivePromise)p).tryProgress(progress, e.Total);
            //}
        }

        /// <summary>
        ///     Will remove the current message, mark its {@link ChannelPromise} as success and return {@code true}. If no
        ///     flushed message exists at the time this method is called it will return {@code false} to signal that no more
        ///     messages are ready to be handled.
        /// </summary>
        public bool Remove()
        {
            Entry e = this.flushedEntry;
            if (e == null)
            {
                this.ClearNioBuffers();
                return false;
            }
            object msg = e.Message;

            TaskCompletionSource promise = e.Promise;
            int size = e.PendingSize;

            this.RemoveEntry(e);

            if (!e.Cancelled)
            {
                // only release message, notify and decrement if it was not canceled before.
                ReferenceCountUtil.SafeRelease(msg);
                Util.SafeSetSuccess(promise, Logger);
                this.DecrementPendingOutboundBytes(size, false, true);
            }

            // recycle the entry
            e.Recycle();

            return true;
        }

        /// <summary>
        ///     Will remove the current message, mark its {@link ChannelPromise} as failure using the given {@link Exception}
        ///     and return {@code true}. If no   flushed message exists at the time this method is called it will return
        ///     {@code false} to signal that no more messages are ready to be handled.
        /// </summary>
        public bool Remove(Exception cause) => this.Remove0(cause, true);

        bool Remove0(Exception cause, bool notifyWritability)
        {
            Entry e = this.flushedEntry;
            if (e == null)
            {
                this.ClearNioBuffers();
                return false;
            }
            object msg = e.Message;

            TaskCompletionSource promise = e.Promise;
            int size = e.PendingSize;

            this.RemoveEntry(e);

            if (!e.Cancelled)
            {
                // only release message, fail and decrement if it was not canceled before.
                ReferenceCountUtil.SafeRelease(msg);

                Util.SafeSetFailure(promise, cause, Logger);
                if (promise != TaskCompletionSource.Void && !promise.TrySetException(cause))
                {
                    Logger.Warn($"Failed to mark a promise as failure because it's done already: {promise}", cause);
                }
                this.DecrementPendingOutboundBytes(size, false, notifyWritability);
            }

            // recycle the entry
            e.Recycle();

            return true;
        }

        void RemoveEntry(Entry e)
        {
            if (-- this.flushed == 0)
            {
                // processed everything
                this.flushedEntry = null;
                if (e == this.tailEntry)
                {
                    this.tailEntry = null;
                    this.unflushedEntry = null;
                }
            }
            else
            {
                this.flushedEntry = e.Next;
            }
        }

        /// <summary>
        ///     Removes the fully written entries and update the reader index of the partially written entry.
        ///     This operation assumes all messages in this buffer is {@link ByteBuf}.
        /// </summary>
        public void RemoveBytes(long writtenBytes)
        {
            while (true)
            {
                object msg = this.Current;
                if (!(msg is IByteBuffer))
                {
                    Contract.Assert(writtenBytes == 0);
                    break;
                }

                var buf = (IByteBuffer)msg;
                int readerIndex = buf.ReaderIndex;
                int readableBytes = buf.WriterIndex - readerIndex;

                if (readableBytes <= writtenBytes)
                {
                    if (writtenBytes != 0)
                    {
                        this.Progress(readableBytes);
                        writtenBytes -= readableBytes;
                    }
                    this.Remove();
                }
                else
                {
                    // readableBytes > writtenBytes
                    if (writtenBytes != 0)
                    {
                        buf.SetReaderIndex(readerIndex + (int)writtenBytes);
                        this.Progress(writtenBytes);
                    }
                    break;
                }
            }
            this.ClearNioBuffers();
        }

        // Clear all ByteBuffer from the array so these can be GC'ed.
        // See https://github.com/netty/netty/issues/3837
        void ClearNioBuffers() => NioBuffers.Value.Clear();
        
        ///
        ///Returns an array of direct NIO buffers if the currently pending messages are made of {@link ByteBuf} only.
        ///{@link #nioBufferCount()} and {@link #nioBufferSize()} will return the number of NIO buffers in the returned
        ///array and the total number of readable bytes of the NIO buffers respectively.
        ///<p>
        ///Note that the returned array is reused and thus should not escape
        ///{@link AbstractChannel#doWrite(ChannelOutboundBuffer)}.
        ///Refer to {@link NioSocketChannel#doWrite(ChannelOutboundBuffer)} for an example.
        ///</p>
        ///
        public List<ArraySegment<byte>> GetNioBuffers()
        {
            long nioBufferSize = 0;
            InternalThreadLocalMap threadLocalMap = InternalThreadLocalMap.Get();
            List<ArraySegment<byte>> nioBuffers = NioBuffers.Get(threadLocalMap);
            Entry entry = this.flushedEntry;
            while (this.IsFlushedEntry(entry) && entry.Message is IByteBuffer) {
                if (!entry.Cancelled)
                {
                    var buf = (IByteBuffer)entry.Message;
                    int readerIndex = buf.ReaderIndex;
                    int readableBytes = buf.WriterIndex - readerIndex;

                    if (readableBytes > 0)
                    {
                        if (int.MaxValue - readableBytes < nioBufferSize)
                        {
                            // If the nioBufferSize + readableBytes will overflow an Integer we stop populate the
                            // ByteBuffer array. This is done as bsd/osx don't allow to write more bytes then
                            // Integer.MAX_VALUE with one writev(...) call and so will return 'EINVAL', which will
                            // raise an IOException. On Linux it may work depending on the
                            // architecture and kernel but to be safe we also enforce the limit here.
                            // This said writing more the Integer.MAX_VALUE is not a good idea anyway.
                            //
                            // See also:
                            // - https://www.freebsd.org/cgi/man.cgi?query=write&sektion=2
                            // - http://linux.die.net/man/2/writev
                            break;
                        }
                        nioBufferSize += readableBytes;
                        int count = entry.Count;
                        if (count == -1)
                        {
                            //noinspection ConstantValueVariableUse
                            entry.Count = count = buf.IoBufferCount;
                        }
                        if (count == 1)
                        {
                            ArraySegment<byte> nioBuf = entry.Buffer;
                            if (nioBuf.Array == null)
                            {
                                // cache ByteBuffer as it may need to create a new ByteBuffer instance if its a
                                // derived buffer
                                entry.Buffer = nioBuf = buf.GetIoBuffer(readerIndex, readableBytes);
                            }
                            nioBuffers.Add(nioBuf);
                        }
                        else
                        {
                            ArraySegment<byte>[] nioBufs = entry.Buffers;
                            if (nioBufs == null)
                            {
                                // cached ByteBuffers as they may be expensive to create in terms
                                // of Object allocation
                                entry.Buffers = nioBufs = buf.GetIoBuffers();
                            }
                            foreach (ArraySegment<byte> b in nioBufs)
                            {
                                nioBuffers.Add(b);
                            }
                        }
                    }
                }
                entry = entry.Next;
            }
            this.NioBufferSize = nioBufferSize;

            return nioBuffers;
        }

        /**
         * Returns the number of bytes that can be written out of the {@link ByteBuffer} array that was
         * obtained via {@link #nioBuffers()}. This method <strong>MUST</strong> be called after {@link #nioBuffers()}
         * was called.
         */
        public long NioBufferSize { get; private set; }

        /// <summary>
        ///     Returns an array of direct NIO buffers if the currently pending messages are made of {@link ByteBuf} only.
        ///     {@link #IoBufferCount} and {@link #NioBufferSize} will return the number of NIO buffers in the returned
        ///     array and the total number of readable bytes of the NIO buffers respectively.
        ///     <p>
        ///         Note that the returned array is reused and thus should not escape
        ///         {@link AbstractChannel#doWrite(ChannelOutboundBuffer)}.
        ///         Refer to {@link NioSocketChannel#doWrite(ChannelOutboundBuffer)} for an example.
        ///     </p>
        /// </summary>
        /// <summary>
        ///     Returns {@code true} if and only if {@linkplain #totalPendingWriteBytes() the total number of pending bytes} did
        ///     not exceed the write watermark of the {@link Channel} and
        ///     no {@linkplain #SetUserDefinedWritability(int, bool) user-defined writability flag} has been set to
        ///     {@code false}.
        /// </summary>
        public bool IsWritable => this.unwritable == 0;

        /// <summary>
        ///     Returns {@code true} if and only if the user-defined writability flag at the specified index is set to
        ///     {@code true}.
        /// </summary>
        public bool GetUserDefinedWritability(int index) => (this.unwritable & WritabilityMask(index)) == 0;

        /// <summary>
        ///     Sets a user-defined writability flag at the specified index.
        /// </summary>
        public void SetUserDefinedWritability(int index, bool writable)
        {
            if (writable)
            {
                this.SetUserDefinedWritability(index);
            }
            else
            {
                this.ClearUserDefinedWritability(index);
            }
        }

        void SetUserDefinedWritability(int index)
        {
            int mask = ~WritabilityMask(index);
            while (true)
            {
                int oldValue = this.unwritable;
                int newValue = oldValue & mask;
                if (Interlocked.CompareExchange(ref this.unwritable, newValue, oldValue) == oldValue)
                {
                    if (oldValue != 0 && newValue == 0)
                    {
                        this.FireChannelWritabilityChanged(true);
                    }
                    break;
                }
            }
        }

        void ClearUserDefinedWritability(int index)
        {
            int mask = WritabilityMask(index);
            while (true)
            {
                int oldValue = this.unwritable;
                int newValue = oldValue | mask;
                if (Interlocked.CompareExchange(ref this.unwritable, newValue, oldValue) == oldValue)
                {
                    if (oldValue == 0 && newValue != 0)
                    {
                        this.FireChannelWritabilityChanged(true);
                    }
                    break;
                }
            }
        }

        static int WritabilityMask(int index)
        {
            if (index < 1 || index > 31)
            {
                throw new InvalidOperationException("index: " + index + " (expected: 1~31)");
            }
            return 1 << index;
        }

        void SetWritable(bool invokeLater)
        {
            while (true)
            {
                int oldValue = this.unwritable;
                int newValue = oldValue & ~1;
                if (Interlocked.CompareExchange(ref this.unwritable, newValue, oldValue) == oldValue)
                {
                    if (oldValue != 0 && newValue == 0)
                    {
                        this.FireChannelWritabilityChanged(invokeLater);
                    }
                    break;
                }
            }
        }

        void SetUnwritable(bool invokeLater)
        {
            while (true)
            {
                int oldValue = this.unwritable;
                int newValue = oldValue | 1;
                if (Interlocked.CompareExchange(ref this.unwritable, newValue, oldValue) == oldValue)
                {
                    if (oldValue == 0 && newValue != 0)
                    {
                        this.FireChannelWritabilityChanged(invokeLater);
                    }
                    break;
                }
            }
        }

        void FireChannelWritabilityChanged(bool invokeLater)
        {
            IChannelPipeline pipeline = this.channel.Pipeline;
            if (invokeLater)
            {
                // todo: allocation check
                this.channel.EventLoop.Execute(p => ((IChannelPipeline)p).FireChannelWritabilityChanged(), pipeline);
            }
            else
            {
                pipeline.FireChannelWritabilityChanged();
            }
        }

        /// <summary>
        ///     Returns the number of flushed messages in this {@link ChannelOutboundBuffer}.
        /// </summary>
        public int Count => this.flushed;

        /// <summary>
        ///     Returns {@code true} if there are flushed messages in this {@link ChannelOutboundBuffer} or {@code false}
        ///     otherwise.
        /// </summary>
        public bool IsEmpty => this.flushed == 0;

        internal void FailFlushed(Exception cause, bool notify)
        {
            // Make sure that this method does not reenter.  A listener added to the current promise can be notified by the
            // current thread in the tryFailure() call of the loop below, and the listener can trigger another fail() call
            // indirectly (usually by closing the channel.)
            //
            // See https://github.com/netty/netty/issues/1501
            if (this.inFail)
            {
                return;
            }

            try
            {
                this.inFail = true;
                for (;;)
                {
                    if (!this.Remove0(cause, notify))
                    {
                        break;
                    }
                }
            }
            finally
            {
                this.inFail = false;
            }
        }

        internal void Close(ClosedChannelException cause)
        {
            if (this.inFail)
            {
                this.channel.EventLoop.Execute((buf, ex) => ((ChannelOutboundBuffer)buf).Close((ClosedChannelException)ex),
                    this, cause);
                return;
            }

            this.inFail = true;

            if (this.channel.Open)
            {
                throw new InvalidOperationException("close() must be invoked after the channel is closed.");
            }

            if (!this.IsEmpty)
            {
                throw new InvalidOperationException("close() must be invoked after all flushed writes are handled.");
            }

            // Release all unflushed messages.
            try
            {
                Entry e = this.unflushedEntry;
                while (e != null)
                {
                    // Just decrease; do not trigger any events via DecrementPendingOutboundBytes()
                    int size = e.PendingSize;
                    Interlocked.Add(ref this.totalPendingSize, -size);

                    if (!e.Cancelled)
                    {
                        ReferenceCountUtil.SafeRelease(e.Message);
                        Util.SafeSetFailure(e.Promise, cause, Logger);
                        if (e.Promise != TaskCompletionSource.Void && !e.Promise.TrySetException(cause))
                        {
                            Logger.Warn($"Failed to mark a promise as failure because it's done already: {e.Promise}", cause);
                        }
                    }
                    e = e.RecycleAndGetNext();
                }
            }
            finally
            {
                this.inFail = false;
            }
            this.ClearNioBuffers();
        }

        public long TotalPendingWriteBytes() => Volatile.Read(ref this.totalPendingSize);

        /// <summary>
        ///     Call {@link IMessageProcessor#processMessage(Object)} for each flushed message
        ///     in this {@link ChannelOutboundBuffer} until {@link IMessageProcessor#processMessage(Object)}
        ///     returns {@code false} or there are no more flushed messages to process.
        /// </summary>
        bool IsFlushedEntry(Entry e) => e != null && e != this.unflushedEntry;

        sealed class Entry
        {
            static readonly ThreadLocalPool<Entry> Pool = new ThreadLocalPool<Entry>(h => new Entry(h));

            readonly ThreadLocalPool.Handle handle;
            public Entry Next;
            public object Message;
            public ArraySegment<byte>[] Buffers;
            public ArraySegment<byte> Buffer;
            public TaskCompletionSource Promise;
            public int PendingSize;
            public int Count = -1;
            public bool Cancelled;

            Entry(ThreadLocalPool.Handle handle)
            {
                this.handle = handle;
            }

            public static Entry NewInstance(object msg, int size, TaskCompletionSource promise)
            {
                Entry entry = Pool.Take();
                entry.Message = msg;
                entry.PendingSize = size;
                entry.Promise = promise;
                return entry;
            }

            public int Cancel()
            {
                if (!this.Cancelled)
                {
                    this.Cancelled = true;
                    int pSize = this.PendingSize;

                    // release message and replace with an empty buffer
                    ReferenceCountUtil.SafeRelease(this.Message);
                    this.Message = Unpooled.Empty;

                    this.PendingSize = 0;
                    this.Buffers = null;
                    this.Buffer = new ArraySegment<byte>();
                    return pSize;
                }
                return 0;
            }

            public void Recycle()
            {
                this.Next = null;
                this.Buffers = null;
                this.Buffer = new ArraySegment<byte>();
                this.Message = null;
                this.Promise = null;
                this.PendingSize = 0;
                this.Count = -1;
                this.Cancelled = false;
                this.handle.Release(this);
            }

            public Entry RecycleAndGetNext()
            {
                Entry next = this.Next;
                this.Recycle();
                return next;
            }
        }

        sealed class ThreadLocalByteBufferList : FastThreadLocal<List<ArraySegment<byte>>>
        {
            protected override List<ArraySegment<byte>> GetInitialValue() => new List<ArraySegment<byte>>(1024);
        }
    }
}