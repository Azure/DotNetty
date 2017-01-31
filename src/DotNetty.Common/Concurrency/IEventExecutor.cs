// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public interface IEventExecutor
    {
        /// <summary>
        ///     Returns <c>true</c> if the current <see cref="Thread" /> belongs to this event loop,
        ///     <c>false</c> otherwise.
        /// </summary>
        /// <remarks>
        ///     It is a convenient way to determine whether code can be executed directly or if it
        ///     should be posted for execution to this executor instance explicitly to ensure execution in the loop.
        /// </remarks>
        bool InEventLoop { get; }

        /// <summary>
        ///     Returns <c>true</c> if and only if this executor is being shut down via <see cref="ShutdownGracefullyAsync()" />.
        /// </summary>
        bool IsShuttingDown { get; }

        /// <summary>
        ///     Gets a <see cref="Task" /> object that represents the asynchronous completion of this executor's termination.
        /// </summary>
        Task TerminationCompletion { get; }

        /// <summary>
        ///     Returns <c>true</c> if this executor has been shut down, <c>false</c> otherwise.
        /// </summary>
        bool IsShutdown { get; }

        /// <summary>
        ///     Returns <c>true</c> if all tasks have completed following shut down.
        /// </summary>
        /// <remarks>
        ///     Note that <see cref="IsTerminated" /> is never <c>true</c> unless <see cref="ShutdownGracefullyAsync()" /> was called first.
        /// </remarks>
        bool IsTerminated { get; }

        /// <summary>
        /// Parent <see cref="IEventExecutorGroup"/>.
        /// </summary>
        IEventExecutorGroup Parent { get; }

        /// <summary>
        ///     Returns <c>true</c> if the given <see cref="Thread" /> belongs to this event loop,
        ///     <c>false></c> otherwise.
        /// </summary>
        bool IsInEventLoop(Thread thread);

        /// <summary>
        ///     Executes the given task.
        /// </summary>
        /// <remarks>Threading specifics are determined by <c>IEventExecutor</c> implementation.</remarks>
        void Execute(IRunnable task);

        /// <summary>
        ///     Executes the given action.
        /// </summary>
        /// <remarks>
        ///     <paramref name="state" /> parameter is useful to when repeated execution of an action against
        ///     different objects is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        void Execute(Action<object> action, object state);

        /// <summary>
        ///     Executes the given <paramref name="action" />.
        /// </summary>
        /// <remarks>Threading specifics are determined by <c>IEventExecutor</c> implementation.</remarks>
        void Execute(Action action);

        /// <summary>
        ///     Executes the given action.
        /// </summary>
        /// <remarks>
        ///     <paramref name="context" /> and <paramref name="state" /> parameters are useful when repeated execution of
        ///     an action against different objects in different context is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        void Execute(Action<object, object> action, object context, object state);

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

        /// <summary>
        ///     Executes the given function and returns <see cref="Task{T}" /> indicating completion status and result of
        ///     execution.
        /// </summary>
        /// <remarks>
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task<T> SubmitAsync<T>(Func<T> func);

        /// <summary>
        ///     Executes the given action and returns <see cref="Task{T}" /> indicating completion status and result of execution.
        /// </summary>
        /// <remarks>
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken);

        /// <summary>
        ///     Executes the given action and returns <see cref="Task{T}" /> indicating completion status and result of execution.
        /// </summary>
        /// <remarks>
        ///     <paramref name="state" /> parameter is useful to when repeated execution of an action against
        ///     different objects is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task<T> SubmitAsync<T>(Func<object, T> func, object state);

        /// <summary>
        ///     Executes the given action and returns <see cref="Task{T}" /> indicating completion status and result of execution.
        /// </summary>
        /// <remarks>
        ///     <paramref name="state" /> parameter is useful to when repeated execution of an action against
        ///     different objects is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task<T> SubmitAsync<T>(Func<object, T> func, object state, CancellationToken cancellationToken);

        /// <summary>
        ///     Executes the given action and returns <see cref="Task{T}" /> indicating completion status and result of execution.
        /// </summary>
        /// <remarks>
        ///     <paramref name="context" /> and <paramref name="state" /> parameters are useful when repeated execution of
        ///     an action against different objects in different context is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state);

        /// <summary>
        ///     Executes the given action and returns <see cref="Task{T}" /> indicating completion status and result of execution.
        /// </summary>
        /// <remarks>
        ///     <paramref name="context" /> and <paramref name="state" /> parameters are useful when repeated execution of
        ///     an action against different objects in different context is needed.
        ///     <para>Threading specifics are determined by <c>IEventExecutor</c> implementation.</para>
        /// </remarks>
        Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state, CancellationToken cancellationToken);

        /// <summary>
        ///     Shortcut method for <see cref="ShutdownGracefullyAsync(TimeSpan,TimeSpan)" /> with sensible default values.
        /// </summary>
        Task ShutdownGracefullyAsync();

        /// <summary>
        ///     Signals this executor that the caller wants the executor to be shut down.  Once this method is called,
        ///     <see cref="IsShuttingDown" /> starts to return <c>true</c>, and the executor prepares to shut itself down.
        ///     Graceful shutdown ensures that no tasks are submitted for <i>'the quiet period'</i>
        ///     (usually a couple seconds) before it shuts itself down.  If a task is submitted during the quiet period,
        ///     it is guaranteed to be accepted and the quiet period will start over.
        /// </summary>
        /// <param name="quietPeriod">the quiet period as described in the documentation.</param>
        /// <param name="timeout">
        ///     the maximum amount of time to wait until the executor <see cref="IsShutdown" />
        ///     regardless if a task was submitted during the quiet period.
        /// </param>
        /// <returns>the <see cref="TerminationCompletion" /> task.</returns>
        Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout);
    }
}