// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class AbstractEventExecutorGroup : IEventExecutorGroup
    {
        static readonly TimeSpan DefaultShutdownQuietPeriod = TimeSpan.FromSeconds(2);
        static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(15);

        public abstract bool IsShutdown { get; }

        public abstract bool IsTerminated { get; }

        public abstract bool IsShuttingDown { get; }

        public abstract Task TerminationCompletion { get; }

        public abstract IEventExecutor GetNext();

        public void Execute(IRunnable task) => this.GetNext().Execute(task);

        public void Execute(Action<object> action, object state) => this.GetNext().Execute(action, state);

        public void Execute(Action action) => this.GetNext().Execute(action);

        public void Execute(Action<object, object> action, object context, object state) => this.GetNext().Execute(action, context, state);

        public Task<T> SubmitAsync<T>(Func<T> func) => this.GetNext().SubmitAsync(func);

        public Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken) => this.GetNext().SubmitAsync(func, cancellationToken);

        public Task<T> SubmitAsync<T>(Func<object, T> func, object state) => GetNext().SubmitAsync(func, state);

        public Task<T> SubmitAsync<T>(Func<object, T> func, object state, CancellationToken cancellationToken) => this.GetNext().SubmitAsync(func, state, cancellationToken);

        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state) => this.GetNext().SubmitAsync(func, context, state);

        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state, CancellationToken cancellationToken) => this.GetNext().SubmitAsync(func, context, cancellationToken);

        public IScheduledTask Schedule(IRunnable action, TimeSpan delay) => this.GetNext().Schedule(action, delay);

        public IScheduledTask Schedule(Action action, TimeSpan delay) => this.GetNext().Schedule(action, delay);

        public IScheduledTask Schedule(Action<object> action, object state, TimeSpan delay) => this.GetNext().Schedule(action, state, delay);

        public IScheduledTask Schedule(Action<object, object> action, object context, object state, TimeSpan delay) => this.GetNext().Schedule(action, context, state, delay);

        public Task ScheduleAsync(Action<object> action, object state, TimeSpan delay, CancellationToken cancellationToken) => this.GetNext().ScheduleAsync(action, state, delay, cancellationToken);

        public Task ScheduleAsync(Action<object> action, object state, TimeSpan delay) => this.GetNext().ScheduleAsync(action, state, delay);

        public Task ScheduleAsync(Action action, TimeSpan delay, CancellationToken cancellationToken) => this.GetNext().ScheduleAsync(action, delay, cancellationToken);

        public Task ScheduleAsync(Action action, TimeSpan delay) => this.GetNext().ScheduleAsync(action, delay);

        public Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay) => this.GetNext().ScheduleAsync(action, context, state, delay);

        public Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken) => this.GetNext().ScheduleAsync(action, context, state, delay);

        public Task ShutdownGracefullyAsync() => this.ShutdownGracefullyAsync(DefaultShutdownQuietPeriod, DefaultShutdownTimeout);

        public abstract Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout);
    }
}