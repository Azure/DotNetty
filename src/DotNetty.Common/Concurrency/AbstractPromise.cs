// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
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

        Action<object> callback;
        object callbackState;
        
        public bool TryComplete() => this.TryComplete0(CompletedNoException);
        
        public bool TrySetException(Exception exception) => this.TryComplete0(exception);

        public bool TrySetCanceled() => this.TryComplete0(CanceledException); 
        
        protected virtual bool TryComplete0(Exception exception)
        {
            if (this.exception == null)
            {
                // Set the exception object to the exception passed in or a sentinel value
                this.exception = exception;
                this.TryExecuteCallback();
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
            this.callback = continuation;
            this.callbackState = state;
            //todo: context preservation

            if (this.exception != null)
            {
                this.TryExecuteCallback();
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

        bool TryExecuteCallback()
        {
            if (this.callback == null)
            {
                return false;
            }

            try
            {
                this.callback(this.callbackState);
                return true;
            }
            finally
            {
                this.ClearCallback();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ClearCallback()
        {
            this.callback = null;
            this.callbackState = null;
        }
    }
}