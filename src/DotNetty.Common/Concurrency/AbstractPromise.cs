// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Sources;

    public abstract class AbstractPromise : IPromise, IValueTaskSource
    {
        struct CompletionData
        {
            public Action<object> Continuation { get; }
            public object State { get; }
            public ExecutionContext ExecutionContext { get; }
            public SynchronizationContext SynchronizationContext { get; }

            public CompletionData(Action<object> continuation, object state, ExecutionContext executionContext, SynchronizationContext synchronizationContext)
            {
                this.Continuation = continuation;
                this.State = state;
                this.ExecutionContext = executionContext;
                this.SynchronizationContext = synchronizationContext;
            }
        }
        
        const short SourceToken = 0;

        static readonly ContextCallback ExecutionContextCallback = Execute;
        static readonly SendOrPostCallback SyncContextCallbackWithExecutionContext = ExecuteWithExecutionContext;
        static readonly SendOrPostCallback SyncContextCallback = Execute;
        
        static readonly Exception CanceledException = new OperationCanceledException();
        static readonly Exception CompletedNoException = new Exception();

        protected Exception exception;
        
        int callbackCount;
        CompletionData[] completions;
        
        public bool TryComplete() => this.TryComplete0(CompletedNoException);
        
        public bool TrySetException(Exception exception) => this.TryComplete0(exception);

        public bool TrySetCanceled() => this.TryComplete0(CanceledException); 
        
        protected virtual bool TryComplete0(Exception exception)
        {
            if (this.exception == null)
            {
                // Set the exception object to the exception passed in or a sentinel value
                this.exception = exception;
                this.TryExecuteCompletions();
                return true;
            }

            return false;
        }

        public bool SetUncancellable() => true;
        
        public virtual ValueTaskSourceStatus GetStatus(short token)
        {
            if (this.exception == null)
            {
                return ValueTaskSourceStatus.Pending;
            }
            else if (this.exception == CompletedNoException)
            {
                return ValueTaskSourceStatus.Succeeded;
            }
            else if (this.exception == CanceledException)
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
            if (this.exception == null)
            {
                throw new InvalidOperationException("Attempt to get result on not yet completed promise");
            }

            this.IsCompletedOrThrow();
        }

        public virtual void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (this.completions == null)
            {
                this.completions = new CompletionData[1];
            }

            int newIndex = this.callbackCount;
            this.callbackCount++;

            if (newIndex == this.completions.Length)
            {
                var newArray = new CompletionData[this.completions.Length * 2];
                Array.Copy(this.completions, newArray, this.completions.Length);
                this.completions = newArray;
            }

            this.completions[newIndex] = new CompletionData(
                continuation, 
                state,
                (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0 ? ExecutionContext.Capture() : null,
                (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0 ? SynchronizationContext.Current : null
            );

            if (this.exception != null)
            {
                this.TryExecuteCompletions();
            }
        }

        public static implicit operator ValueTask(AbstractPromise promise) => new ValueTask(promise, SourceToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsCompletedOrThrow()
        {
            if (this.exception == null)
            {
                return false;
            }

            if (this.exception != CompletedNoException)
            {
                this.ThrowLatchedException();
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void ThrowLatchedException() => ExceptionDispatchInfo.Capture(this.exception).Throw();

        bool TryExecuteCompletions()
        {
            if (this.callbackCount == 0 || this.completions == null)
            {
                return false;
            }

            List<Exception> exceptions = null;
            
            for (int i = 0; i < this.callbackCount; i++)
            {
                try
                {
                    CompletionData completion = this.completions[i];
                    ExecuteCompletion(completion);
                }
                catch (Exception ex)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }

                    exceptions.Add(ex);
                }
            }

            if (exceptions == null)
            {
                return true;
            }
            
            throw new AggregateException(exceptions);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ClearCallbacks()
        {
            if (this.callbackCount > 0)
            {
                this.callbackCount = 0;
                Array.Clear(this.completions, 0, this.completions.Length);
            }
        }        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ExecuteCompletion(CompletionData completion)
        {
            if (completion.SynchronizationContext == null)
            {
                if (completion.ExecutionContext == null)
                {
                    completion.Continuation(completion.State);
                }
                else
                {
                    //boxing
                    ExecutionContext.Run(completion.ExecutionContext, ExecutionContextCallback, completion);    
                }
            }
            else
            {
                if (completion.ExecutionContext == null)
                {
                    //boxing
                    completion.SynchronizationContext.Post(SyncContextCallback, completion);
                }
                else
                {
                    //boxing
                    completion.SynchronizationContext.Post(SyncContextCallbackWithExecutionContext, completion);
                }
            }
        }

        static void Execute(object state)
        {
            CompletionData completion = (CompletionData)state;
            completion.Continuation(completion.State);
        }
        
        static void ExecuteWithExecutionContext(object state)
        {
            CompletionData completion = (CompletionData)state;
            ExecutionContext.Run(completion.ExecutionContext, ExecutionContextCallback, state);
        }
    }
}