// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IExecutorService : IExecutor
    {
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
    }
}