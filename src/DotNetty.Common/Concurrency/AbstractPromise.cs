// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Sources;

    public abstract class AbstractPromise : IPromise, IValueTaskSource
    {
        const short SourceToken = 0;
        
        static readonly Exception CanceledException = new OperationCanceledException();
        static readonly Exception CompletedNoException = new Exception();
       
        protected Exception exception;
        
        protected int callbackCount;
        protected (Action<object>, object)[] callbacks;
        
        public bool TryComplete() => this.TryComplete0(CompletedNoException);
        
        public bool TrySetException(Exception exception) => this.TryComplete0(exception);

        public bool TrySetCanceled() => this.TryComplete0(CanceledException); 
        
        protected virtual bool TryComplete0(Exception exception)
        {
            if (this.exception == null)
            {
                // Set the exception object to the exception passed in or a sentinel value
                this.exception = exception;
                this.ExecuteCallbacks();
                return true;
            }

            return false;
        }

        public IValueTaskSource Future => this;

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
                //ThrowHelper.ThrowInvalidOperationException_GetResultNotCompleted();
            }

            this.IsCompletedOrThrow();
            /*
                // Change the state from to be canceled -> observed
                if (_writerAwaitable.ObserveCancelation())
                {
                    result._resultFlags |= ResultFlags.Canceled;
                }
                if (_readerCompletion.IsCompletedOrThrow())
                {
                    result._resultFlags |= ResultFlags.Completed;
                }
            */
        }

        public virtual void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (this.callbacks == null)
            {
                this.callbacks = new (Action<object>, object)[1];
                //this.callbacks = s_completionCallbackPool.Rent(InitialCallbacksSize);
            }

            int newIndex = this.callbackCount;
            this.callbackCount++;

            if (newIndex == this.callbacks.Length)
            {
                var newArray = new (Action<object>, object)[this.callbacks.Length * 2];
                Array.Copy(this.callbacks, newArray, this.callbacks.Length);
                this.callbacks = newArray;
            }

            this.callbacks[newIndex] = (continuation, state);

            if (this.exception != null)
            {
                this.ExecuteCallbacks();
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

        void ExecuteCallbacks()
        {
            if (this.callbacks == null || this.callbackCount == 0)
            {
                return;
            }

            try
            {
                List<Exception> exceptions = null;

                for (int i = 0; i < this.callbackCount; i++)
                {
                    try
                    {
                        (Action<object> callback, object state) = this.callbacks[i];
                        callback(state);
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

                if (exceptions != null)
                {
                    throw new AggregateException(exceptions);
                }
            }
            finally
            {
                this.ClearCallbacks();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ClearCallbacks()
        {
            if (this.callbackCount > 0)
            {
                this.callbackCount = 0;
                Array.Clear(this.callbacks, 0, this.callbacks.Length);
            }
        }
    }
}