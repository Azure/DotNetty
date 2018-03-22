// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public sealed class ReadListeningHandler : ChannelHandlerAdapter
    {
        readonly Queue<object> receivedQueue = new Queue<object>();
        readonly Queue<TaskCompletionSource<object>> readPromises = new Queue<TaskCompletionSource<object>>();
        readonly TimeSpan defaultReadTimeout;
        readonly object gate = new object();

        volatile Exception registeredException;

        public ReadListeningHandler()
            : this(TimeSpan.Zero)
        {
        }

        public ReadListeningHandler(TimeSpan defaultReadTimeout)
        {
            this.defaultReadTimeout = defaultReadTimeout;
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            lock (this.gate)
            {
                if (this.readPromises.Count > 0)
                {
                    TaskCompletionSource<object> promise = this.readPromises.Dequeue();
                    promise.TrySetResult(message);
                }
                else
                {
                    this.receivedQueue.Enqueue(message);
                }
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.SetException(new InvalidOperationException("Channel is closed."));
            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => this.SetException(exception);

        void SetException(Exception exception)
        {
            this.registeredException = exception;

            lock (this.gate)
            {
                while (this.readPromises.Count > 0)
                {
                    TaskCompletionSource<object> promise = this.readPromises.Dequeue();
                    promise.TrySetException(exception);
                }
            }
        }

        public async Task<object> ReceiveAsync(TimeSpan timeout = default(TimeSpan))
        {
            if (this.registeredException != null)
            {
                throw this.registeredException;
            }

            var promise = new TaskCompletionSource<object>();

            lock (this.gate)
            {
                if (this.receivedQueue.Count > 0)
                {
                    return this.receivedQueue.Dequeue();
                }

                this.readPromises.Enqueue(promise);
            }

            timeout = timeout <= TimeSpan.Zero ? this.defaultReadTimeout : timeout;
            if (timeout > TimeSpan.Zero)
            {
                Task task = await Task.WhenAny(promise.Task, Task.Delay(timeout));
                if (task != promise.Task)
                {
                    throw new TimeoutException("ReceiveAsync timed out");
                }

                return promise.Task.Result;
            }

            return await promise.Task;
        }
    }
}