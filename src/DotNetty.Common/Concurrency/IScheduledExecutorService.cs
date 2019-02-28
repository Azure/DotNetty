// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IScheduledExecutorService : IExecutorService
    {
        /// <summary>
        ///     Creates and executes a one-shot action that becomes enabled after the given delay.
        /// </summary>
        /// <param name="action">the task to execute</param>
        /// <param name="delay">the time from now to delay execution</param>
        /// <returns>an <see cref="IScheduledTask" /> representing pending completion of the task.</returns>
        IScheduledTask Schedule(IRunnable action, TimeSpan delay);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        IScheduledTask Schedule(Action action, TimeSpan delay);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <paramref name="state" /> parameter is useful to when repeated execution of an action against
        ///     different objects is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        IScheduledTask Schedule(Action<object> action, object state, TimeSpan delay);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <paramref name="context" /> and <paramref name="state" /> parameters are useful when repeated execution of
        ///     an action against different objects in different context is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        IScheduledTask Schedule(Action<object, object> action, object context, object state, TimeSpan delay);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <paramref name="state" /> parameter is useful to when repeated execution of an action against
        ///     different objects is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <paramref name="state" /> parameter is useful to when repeated execution of an action against
        ///     different objects is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task ScheduleAsync(Action<object> action, object state, TimeSpan delay);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task ScheduleAsync(Action action, TimeSpan delay, CancellationToken cancellationToken);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task ScheduleAsync(Action action, TimeSpan delay);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <paramref name="context" /> and <paramref name="state" /> parameters are useful when repeated execution of
        ///     an action against different objects in different context is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay);

        /// <summary>
        ///     Schedules the given action for execution after the specified delay would pass.
        /// </summary>
        /// <remarks>
        ///     <paramref name="context" /> and <paramref name="state" /> parameters are useful when repeated execution of
        ///     an action against different objects in different context is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken);
    }
}