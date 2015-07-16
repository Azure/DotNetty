// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Diagnostics.Contracts;

    public abstract class RecyclableMpscLinkedQueueNode<T> : MpscLinkedQueueNode<T>
    {
        readonly ThreadLocalPool.Handle handle;

        protected RecyclableMpscLinkedQueueNode(ThreadLocalPool.Handle handle)
        {
            Contract.Requires(handle != null);
            this.handle = handle;
        }

        internal override void Unlink()
        {
            base.Unlink();
            this.handle.Release(this);
        }
    }
}