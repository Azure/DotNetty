// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;

    public abstract class AbstractChannelPromise : IChannelFuture, IChannelPromise
    {
        protected static readonly Exception CompletedNoException = new Exception();
        
        protected Exception exception;
        
        protected int callbackCount;
        protected (Delegate, object)[] callbacks;
        
        public virtual bool IsCompleted => this.exception != null;

        public virtual bool TryComplete(Exception exception = null)
        {
            if (this.exception == null)
            {
                // Set the exception object to the exception passed in or a sentinel value
                this.exception = exception ?? CompletedNoException;
                this.ExecuteCallbacks();
                return true;
            }

            return false;
        }

        public IChannelFuture Future => this;

        public bool SetUncancellable() => true;

        public virtual void GetResult()
        {
            if (!this.IsCompleted)
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

        public void UnsafeOnCompleted(Action continuation) => this.OnCompleted(continuation);

        public void OnCompleted(Action callback) => this.OnCompleted0(callback, null);

        public void OnCompleted(Action<object> continuation, object state) => this.OnCompleted0(continuation, null);

        protected virtual void OnCompleted0(Delegate callback, object state)
        {
            if (this.callbacks == null)
            {
                this.callbacks = new (Delegate, object)[1];
                //this.callbacks = s_completionCallbackPool.Rent(InitialCallbacksSize);
            }

            int newIndex = this.callbackCount;
            this.callbackCount++;

            if (newIndex == this.callbacks.Length)
            {
                var newArray = new (Delegate, object)[this.callbacks.Length * 2];
                Array.Copy(this.callbacks, newArray, this.callbacks.Length);
                this.callbacks = newArray;
            }

            this.callbacks[newIndex] = (callback, state);

            if (this.IsCompleted)
            {
                this.ExecuteCallbacks();
            }
        }

        public static implicit operator ChannelFuture(AbstractChannelPromise promise) => new ChannelFuture(promise);

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
                        (Delegate callback, object state) = this.callbacks[i];
                        switch (callback)
                        {
                            case Action action:
                                action();
                                break;
                            case Action<object> action:
                                action(state);
                                break;
                            default:
                                throw new ArgumentException("action");
                        }
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