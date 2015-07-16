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