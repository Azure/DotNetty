// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices.ComTypes;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Sources;

    public abstract class AbstractPromise : IPromise, IValueTaskSource
    {
        static readonly ContextCallback ExecutionContextCallback = Execute;
        static readonly SendOrPostCallback SyncContextCallback = Execute;
        static readonly SendOrPostCallback SyncContextCallbackWithExecutionContext = ExecuteWithExecutionContext;
        static readonly Action<object> TaskSchedulerCallback = Execute;
        static readonly Action<object> TaskScheduleCallbackWithExecutionContext = ExecuteWithExecutionContext;
        
        static readonly Exception CompletedSentinel = new Exception();

        short currentId;
        protected Exception exception;

        Action<object> continuation;
        object state;
        ExecutionContext executionContext;
        object schedulingContext;

        public ValueTask ValueTask => new ValueTask(this, this.currentId);
        
        public bool TryComplete() => this.TryComplete0(CompletedSentinel, out _);
        
        public bool TrySetException(Exception exception) => this.TryComplete0(exception, out _);

        public bool TrySetCanceled(CancellationToken cancellationToken = default(CancellationToken)) => this.TryComplete0(new OperationCanceledException(cancellationToken), out _); 
        
        protected virtual bool TryComplete0(Exception exception, out bool continuationInvoked)
        {
            continuationInvoked = false;
            
            if (this.exception == null)
            {
                // Set the exception object to the exception passed in or a sentinel value
                this.exception = exception;

                if (this.continuation != null)
                {
                    this.ExecuteContinuation();
                    continuationInvoked = true;
                }
                return true;
            }

            return false;
        }

        public bool SetUncancellable() => true;
        
        public virtual ValueTaskSourceStatus GetStatus(short token)
        {
            this.EnsureValidToken(token);
            
            if (this.exception == null)
            {
                return ValueTaskSourceStatus.Pending;
            }
            else if (this.exception == CompletedSentinel)
            {
                return ValueTaskSourceStatus.Succeeded;
            }
            else if (this.exception is OperationCanceledException)
            {
                return ValueTaskSourceStatus.Canceled;
            }
            else
            {
                return ValueTaskSourceStatus.Faulted;
            }
        }

        public virtual void GetResult(short token)
        {
            this.EnsureValidToken(token);

            if (this.exception == null)
            {
                throw new InvalidOperationException("Attempt to get result on not yet completed promise");
            }

            this.currentId++;

            if (this.exception != CompletedSentinel)
            {
                this.ThrowLatchedException();
            }
        }

        public virtual void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            this.EnsureValidToken(token);

            if (this.continuation != null)
            {
                throw new InvalidOperationException("Attempt to subscribe same promise twice");
            }

            this.continuation = continuation;
            this.state = state;
            this.executionContext = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0 ? ExecutionContext.Capture() : null;

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                SynchronizationContext sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    this.schedulingContext = sc;
                }
                else
                {
                    TaskScheduler ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        this.schedulingContext = ts;
                    }
                }
            }

            if (this.exception != null)
            {
                this.ExecuteContinuation();
            }
        }

        public static implicit operator ValueTask(AbstractPromise promise) => promise.ValueTask;

        [MethodImpl(MethodImplOptions.NoInlining)]
        void ThrowLatchedException() => ExceptionDispatchInfo.Capture(this.exception).Throw();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ClearCallback()
        {
            this.continuation = null;
            this.state = null;
            this.executionContext = null;
            this.schedulingContext = null;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureValidToken(short token)
        {
            if (this.currentId != token)
            {
                throw new InvalidOperationException("Incorrect ValueTask token");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ExecuteContinuation()
        {
            ExecutionContext executionContext = this.executionContext;
            object schedulingContext = this.schedulingContext;
            
            if (schedulingContext == null)
            {
                if (executionContext == null)
                {
                    this.ExecuteContinuation0();
                }
                else
                {
                    ExecutionContext.Run(executionContext, ExecutionContextCallback, this);    
                }
            }
            else if (schedulingContext is SynchronizationContext sc)
            {
                sc.Post(executionContext == null ? SyncContextCallback : SyncContextCallbackWithExecutionContext, this);
            }
            else
            {
                TaskScheduler ts = (TaskScheduler)schedulingContext;
                Contract.Assert(ts != null, "Expected a TaskScheduler");
                Task.Factory.StartNew(executionContext == null ? TaskSchedulerCallback : TaskScheduleCallbackWithExecutionContext, this, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
            }
        }

        static void Execute(object state) => ((AbstractPromise)state).ExecuteContinuation0();

        static void ExecuteWithExecutionContext(object state) => ExecutionContext.Run(((AbstractPromise)state).executionContext, ExecutionContextCallback, state);

        protected virtual void ExecuteContinuation0()
        {
            Contract.Assert(this.continuation != null);
            this.continuation(this.state);
        }
    }
}