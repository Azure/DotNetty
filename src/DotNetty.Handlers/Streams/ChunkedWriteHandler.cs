// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Streams
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class ChunkedWriteHandler<T> : ChannelDuplexHandler
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ChunkedWriteHandler<T>>();

        readonly Queue<PendingWrite> queue = new Queue<PendingWrite>();
        volatile IChannelHandlerContext ctx;
        PendingWrite currentWrite;

        public override void HandlerAdded(IChannelHandlerContext context) => this.ctx = context;

        public void ResumeTransfer()
        {
            if (this.ctx == null)
            {
                return;
            }

            if (this.ctx.Executor.InEventLoop)
            {
                this.InvokeDoFlush(this.ctx);
            }
            else
            {
                this.ctx.Executor.Execute(state => this.InvokeDoFlush((IChannelHandlerContext)state), this.ctx);
            }
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            var pendingWrite = new PendingWrite(message);
            this.queue.Enqueue(pendingWrite);
            return pendingWrite.PendingTask;
        }

        public override void Flush(IChannelHandlerContext context) => this.DoFlush(context);

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.DoFlush(context);
            context.FireChannelInactive();
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            if (context.Channel.IsWritable)
            {
                // channel is writable again try to continue flushing
                this.DoFlush(context);
            }

            context.FireChannelWritabilityChanged();
        }

        void Discard(Exception cause = null)
        {
            for (;;)
            {
                PendingWrite current = this.currentWrite;
                if (this.currentWrite == null)
                {
                    current = this.queue.Count > 0 ? this.queue.Dequeue() : null;
                }
                else
                {
                    this.currentWrite = null;
                }

                if (current == null)
                {
                    break;
                }

                object message = current.Message;
                var chunks = message as IChunkedInput<T>;
                if (chunks != null)
                {
                    try
                    {
                        if (!chunks.IsEndOfInput)
                        {
                            if (cause == null)
                            {
                                cause = new ClosedChannelException();
                            }

                            current.Fail(cause);
                        }
                        else
                        {
                            current.Success();
                        }
                    }
                    catch (Exception exception)
                    {
                        current.Fail(exception);
                        Logger.Warn($"{StringUtil.SimpleClassName(typeof(ChunkedWriteHandler<T>))}.IsEndOfInput failed", exception);
                    }
                    finally
                    {
                        CloseInput(chunks);
                    }
                }
                else
                {
                    if (cause == null)
                    {
                        cause = new ClosedChannelException();
                    }

                    current.Fail(cause);
                }
            }
        }

        void InvokeDoFlush(IChannelHandlerContext context)
        {
            try
            {
                this.DoFlush(context);
            }
            catch (Exception exception)
            {
                if (Logger.WarnEnabled)
                {
                    Logger.Warn("Unexpected exception while sending chunks.", exception);
                }
            }
        }

        void DoFlush(IChannelHandlerContext context)
        {
            IChannel channel = context.Channel;
            if (!channel.Active)
            {
                this.Discard();
                return;
            }

            bool requiresFlush = true;
            IByteBufferAllocator allocator = context.Allocator;
            while (channel.IsWritable)
            {
                if (this.currentWrite == null)
                {
                    this.currentWrite = this.queue.Count > 0 ? this.queue.Dequeue() : null;
                }

                if (this.currentWrite == null)
                {
                    break;
                }

                PendingWrite current = this.currentWrite;
                object pendingMessage = current.Message;

                var chunks = pendingMessage as IChunkedInput<T>;
                if (chunks != null)
                {
                    bool endOfInput;
                    bool suspend;
                    object message = null;

                    try
                    {
                        message = chunks.ReadChunk(allocator);
                        endOfInput = chunks.IsEndOfInput;
                        if (message == null)
                        {
                            // No need to suspend when reached at the end.
                            suspend = !endOfInput;
                        }
                        else
                        {
                            suspend = false;
                        }
                    }
                    catch (Exception exception)
                    {
                        this.currentWrite = null;

                        if (message != null)
                        {
                            ReferenceCountUtil.Release(message);
                        }

                        current.Fail(exception);
                        CloseInput(chunks);

                        break;
                    }

                    if (suspend)
                    {
                        // ChunkedInput.nextChunk() returned null and it has
                        // not reached at the end of input. Let's wait until
                        // more chunks arrive. Nothing to write or notify.
                        break;
                    }

                    if (message == null)
                    {
                        // If message is null write an empty ByteBuf.
                        // See https://github.com/netty/netty/issues/1671
                        message = Unpooled.Empty;
                    }

                    Task future = context.WriteAsync(message);
                    if (endOfInput)
                    {
                        this.currentWrite = null;

                        // Register a listener which will close the input once the write is complete.
                        // This is needed because the Chunk may have some resource bound that can not
                        // be closed before its not written.
                        //
                        // See https://github.com/netty/netty/issues/303
                        future.ContinueWith((_, state) =>
                            {
                                var pendingTask = (PendingWrite)state;
                                CloseInput((IChunkedInput<T>)pendingTask.Message);
                                pendingTask.Success();
                            },
                            current, 
                            TaskContinuationOptions.ExecuteSynchronously);
                    }
                    else if (channel.IsWritable)
                    {
                        future.ContinueWith((task, state) =>
                            {
                                var pendingTask = (PendingWrite)state;
                                if (task.IsFaulted)
                                {
                                    CloseInput((IChunkedInput<T>)pendingTask.Message);
                                    pendingTask.Fail(task.Exception);
                                }
                                else
                                {
                                    pendingTask.Progress(chunks.Progress, chunks.Length);
                                }
                            },
                            current,
                            TaskContinuationOptions.ExecuteSynchronously);
                    }
                    else
                    {
                        future.ContinueWith((task, state) =>
                        {
                            var handler = (ChunkedWriteHandler<T>) state;
                            if (task.IsFaulted)
                            {
                                CloseInput((IChunkedInput<T>)handler.currentWrite.Message);
                                handler.currentWrite.Fail(task.Exception);
                            }
                            else
                            {
                                handler.currentWrite.Progress(chunks.Progress, chunks.Length);
                                if (channel.IsWritable)
                                {
                                    handler.ResumeTransfer();
                                }
                            }
                        },
                        this,
                        TaskContinuationOptions.ExecuteSynchronously);
                    }

                    // Flush each chunk to conserve memory
                    context.Flush();
                    requiresFlush = false;
                }
                else
                {
                    context.WriteAsync(pendingMessage)
                        .ContinueWith((task, state) =>
                            {
                                var pendingTask = (PendingWrite)state;
                                if (task.IsFaulted)
                                {
                                    pendingTask.Fail(task.Exception);
                                }
                                else
                                {
                                    pendingTask.Success();
                                }
                            },
                            current,
                            TaskContinuationOptions.ExecuteSynchronously);

                    this.currentWrite = null;
                    requiresFlush = true;
                }

                if (!channel.Active)
                {
                    this.Discard(new ClosedChannelException());
                    break;
                }
            }

            if (requiresFlush)
            {
                context.Flush();
            }
        }
        
        static void CloseInput(IChunkedInput<T> chunks)
        {
            try
            {
                chunks.Close();
            }
            catch (Exception exception)
            {
                if (Logger.WarnEnabled)
                {
                    Logger.Warn("Failed to close a chunked input.", exception);
                }
            }
        }

        sealed class PendingWrite
        {
            readonly TaskCompletionSource promise;

            public PendingWrite(object msg)
            {
                this.Message = msg;
                this.promise = new TaskCompletionSource();
            }

            public object Message { get; }

            public void Success() => this.promise.TryComplete();

            public void Fail(Exception error)
            {
                ReferenceCountUtil.Release(this.Message);
                this.promise.TrySetException(error);
            }

            public void Progress(long progress, long total)
            {
                if (progress < total)
                {
                    return;
                }

                this.Success();
            }

            public Task PendingTask => this.promise.Task;
        }
    }
}
