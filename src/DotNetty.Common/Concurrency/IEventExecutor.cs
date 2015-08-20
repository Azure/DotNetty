// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEventExecutor
    {
        bool InEventLoop { get; }

        bool IsShuttingDown { get; }

        Task TerminationCompletion { get; }

        bool IsShutdown { get; }

        bool IsTerminated { get; }

        bool IsInEventLoop(Thread thread);

        /// <summary>
        ///     Returns an {@link IEventExecutor} that is not an {@link IWrappedEventExecutor}.
        /// </summary>
        /// <remarks>
        ///     <ul>
        ///         <li>
        ///             A {@link IWrappedEventExecutor} implementing this method must return the underlying
        ///             {@link IEventExecutor} while making sure that it's not a {@link IWrappedEventExecutor}
        ///             (e.g. by multiple calls to {@link #unwrap()}).
        ///         </li>
        ///         <li>
        ///             An {@link IEventExecutor} that is not a {@link IWrappedEventExecutor} must return a reference to itself.
        ///         </li>
        ///         <li>
        ///             This method must not return null.
        ///         </li>
        ///     </ul>
        /// </remarks>
        IEventExecutor Unwrap();

        void Execute(IRunnable task);

        void Execute(Action<object> action, object state);

        void Execute(Action action);

        void Schedule(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken);

        void Schedule(Action<object> action, object state, TimeSpan delay);

        void Schedule(Action action, TimeSpan delay, CancellationToken cancellationToken);

        void Schedule(Action action, TimeSpan delay);

        Task SubmitAsync(Func<object, Task> taskFunc, object state);

        Task SubmitAsync(Func<Task> taskFunc);

        void Execute(Action<object, object> action, object state1, object state2);

        Task SubmitAsync(Func<object, object, Task> func, object state1, object state2);

        Task ShutdownGracefullyAsync();

        Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout);
    }
}