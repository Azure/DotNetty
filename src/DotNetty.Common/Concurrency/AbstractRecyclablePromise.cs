// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks.Sources;

    public abstract class AbstractRecyclablePromise : AbstractPromise
    {
        static readonly Action<object> RecycleAction = Recycle;
        
        protected readonly ThreadLocalPool.Handle handle;
        
        protected bool recycled;
        protected IEventExecutor executor;
        
        protected AbstractRecyclablePromise(ThreadLocalPool.Handle handle)
        {
            this.handle = handle;
        }

        public override ValueTaskSourceStatus GetStatus(short token)
        {
            this.ThrowIfRecycled();
            return base.GetStatus(token);
        }

        public override void GetResult(short token)
        {
            this.ThrowIfRecycled();
            base.GetResult(token);
        }

        protected override bool TryComplete0(Exception exception, out bool continuationInvoked)
        {
            Contract.Assert(this.executor.InEventLoop, "must be invoked from an event loop");
            this.ThrowIfRecycled();

            try
            {
                bool completed = base.TryComplete0(exception, out continuationInvoked);
                if (!continuationInvoked)
                {
                    this.Recycle();
                }
                return completed;
            }
            catch
            {
                this.Recycle();
                throw;
            }
        }
        
        protected void Init(IEventExecutor executor)
        {
            this.executor = executor;
            this.recycled = false;
        }

        protected virtual void Recycle()
        {
            Contract.Assert(this.executor.InEventLoop, "must be invoked from an event loop");
            this.exception = null;
            this.ClearCallback();
            this.executor = null;
            this.recycled = true;
            this.handle.Release(this);
        }

        public override void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            this.ThrowIfRecycled();
            base.OnCompleted(continuation,state, token, flags);
        }

        protected override void ExecuteContinuation0()
        {
            try
            {
                base.ExecuteContinuation0();
            }
            finally
            {
                if (this.executor.InEventLoop)
                {
                    this.Recycle();
                }
                else
                {
                    this.executor.Execute(RecycleAction, this);
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ThrowIfRecycled()
        {
            if (this.recycled)
            {
                throw new InvalidOperationException("Attempt to use recycled channel promise");
            }
        }
        
        static void Recycle(object state)
        {
            AbstractRecyclablePromise promise = (AbstractRecyclablePromise)state;
            Contract.Assert(promise.executor.InEventLoop, "must be invoked from an event loop");
            promise.Recycle();
        }
    }
}