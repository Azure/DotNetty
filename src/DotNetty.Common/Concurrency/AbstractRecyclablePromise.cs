// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks.Sources;

    public abstract class AbstractRecyclablePromise : AbstractPromise
    {
        protected IEventExecutor executor;
        protected bool recycled;
        protected readonly ThreadLocalPool.Handle handle;

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

        protected override bool TryComplete0(Exception exception)
        {
            this.ThrowIfRecycled();

            bool completed;
            try
            {
                completed = base.TryComplete0(exception);
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

        public override void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            this.ThrowIfRecycled();
            base.OnCompleted(continuation, state, token, flags);
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