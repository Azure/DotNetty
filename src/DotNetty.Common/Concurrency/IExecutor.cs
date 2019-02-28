// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Common.Concurrency
{
    using System;

    public interface IExecutor
    {
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
    }
}