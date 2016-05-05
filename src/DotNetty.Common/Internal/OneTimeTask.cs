// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     <see cref="IRunnable" /> which represent a one time task which may allow the <see cref="IEventExecutor" /> to
    ///     reduce the amount of
    ///     produced garbage when queue it for execution.
    ///     <strong>It is important this will not be reused. After submitted it is not allowed to get submitted again!</strong>
    /// </summary>
    public abstract class OneTimeTask : MpscLinkedQueueNode<IRunnable>, IRunnable
    {
        public override IRunnable Value => this;

        public abstract void Run();
    }
}