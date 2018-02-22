// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Runtime.CompilerServices;

    public abstract class AbstractRecyclableChannelPromise : AbstractChannelPromise
    {
        protected IEventExecutor executor;
        protected bool recycled;

        protected readonly ThreadLocalPool.Handle handle;

        protected AbstractRecyclableChannelPromise(ThreadLocalPool.Handle handle)
        {
            this.handle = handle;
        }

        public override bool IsCompleted
        {
            get
            {
                this.ThrowIfRecycled();
                return base.IsCompleted;
            }
        }

        public override void GetResult()
        {
            this.ThrowIfRecycled();
            base.GetResult();
        }

        public override bool TryComplete(Exception exception = null)
        {
            this.ThrowIfRecycled();

            bool completed;
            try
            {
                completed = base.TryComplete(exception);
            }
            catch
            {
                this.executor.Execute(this.Recycle);
                throw;
            }

            if (completed)
            {
                this.executor.Execute(this.Recycle);
            }

            return completed;
        }

        protected void Init(IEventExecutor executor)
        {
            this.executor = executor;
            this.recycled = false;
        }

        protected virtual void Recycle()
        {
            this.executor = null;
            this.exception = null;
            this.ClearCallbacks();
            this.recycled = true;

            this.handle.Release(this);
        }

        protected override void OnCompleted0(Delegate callback, object state)
        {
            this.ThrowIfRecycled();
            base.OnCompleted0(callback, state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ThrowIfRecycled()
        {
            if (this.recycled)
            {
                throw new InvalidOperationException("Attempt to use recycled channel promise");
            }

            if (this.executor == null)
            {
                throw new InvalidOperationException("Attempt to use recyclable channel promise without executor");
            }
        }
    }
}