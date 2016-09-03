// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    public static class ExecutionEnvironment
    {
        [ThreadStatic]
        static IEventExecutor currentExecutor;

        public static bool TryGetCurrentExecutor(out IEventExecutor executor)
        {
            executor = currentExecutor;
            return executor != null;
        }

        internal static void SetCurrentExecutor(IEventExecutor executor) => currentExecutor = executor;
    }
}