// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public static class TaskEx
    {
        public static readonly Task<int> Zero = Task.FromResult(0);

        public static readonly Task<int> Completed = Zero;

        public static readonly Task<int> Cancelled = CreateCancelledTask();

        public static readonly Task<bool> True = Task.FromResult(true);

        public static readonly Task<bool> False = Task.FromResult(false);

        public static ValueTask ToValueTask(this Exception ex) => new ValueTask(FromException(ex));
        
        public static ValueTask<T> ToValueTask<T>(this Exception ex) => new ValueTask<T>(FromException<T>(ex));

        static Task<int> CreateCancelledTask()
        {
            var tcs = new TaskCompletionSource<int>();
            tcs.SetCanceled();
            return tcs.Task;
        }

        public static Task FromException(Exception exception)
        {
            var tcs = new TaskCompletionSource();
            tcs.TrySetException(exception);
            return tcs.Task;
        }

        public static Task<T> FromException<T>(Exception exception)
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.TrySetException(exception);
            return tcs.Task;
        }

        static readonly Action<Task, object> LinkOutcomeTcs = (t, tcs) =>
        {
            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    ((TaskCompletionSource)tcs).TryComplete();
                    break;
                case TaskStatus.Canceled:
                    ((TaskCompletionSource)tcs).TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    ((TaskCompletionSource)tcs).TryUnwrap(t.Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        };
        
        public static void LinkOutcome(this Task task, TaskCompletionSource taskCompletionSource)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    taskCompletionSource.TryComplete();
                    break;
                case TaskStatus.Canceled:
                    taskCompletionSource.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    taskCompletionSource.TryUnwrap(task.Exception);
                    break;
                default:
                    task.ContinueWith(LinkOutcomeTcs, taskCompletionSource, TaskContinuationOptions.ExecuteSynchronously);
                    break;
            }
        }
        
        static readonly Action<Task, object> LinkOutcomePromise = (t, promise) =>
        {
            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    ((IPromise)promise).TryComplete();
                    break;
                case TaskStatus.Canceled:
                    ((IPromise)promise).TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    ((IPromise)promise).TryUnwrap(t.Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        };
        
        public static void LinkOutcome(this Task task, IPromise promise)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    promise.TryComplete();
                    break;
                case TaskStatus.Canceled:
                    promise.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    promise.TryUnwrap(task.Exception);
                    break;
                default:
                    task.ContinueWith(LinkOutcomePromise, promise, TaskContinuationOptions.ExecuteSynchronously);
                    break;
            }
        }

        public static async void LinkOutcome(this ValueTask future, IPromise promise)
        {
            try
            {
                //context capturing not required since callback executed synchronously on completion in eventloop
                await future;
                promise.TryComplete();
            }
            catch (Exception ex)
            {
                promise.TrySetException(ex);
            }
        }
        

        static class LinkOutcomeActionHost<T>
        {
            public static readonly Action<Task<T>, object> Action =
                (t, tcs) =>
                {
                    switch (t.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            ((TaskCompletionSource<T>)tcs).TrySetResult(t.Result);
                            break;
                        case TaskStatus.Canceled:
                            ((TaskCompletionSource<T>)tcs).TrySetCanceled();
                            break;
                        case TaskStatus.Faulted:
                            ((TaskCompletionSource<T>)tcs).TryUnwrap(t.Exception);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                };
        }

        public static void LinkOutcome<T>(this Task<T> task, TaskCompletionSource<T> taskCompletionSource)
        {
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    taskCompletionSource.TrySetResult(task.Result);
                    break;
                case TaskStatus.Canceled:
                    taskCompletionSource.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    taskCompletionSource.TryUnwrap(task.Exception);
                    break;
                default:
                    task.ContinueWith(LinkOutcomeActionHost<T>.Action, taskCompletionSource, TaskContinuationOptions.ExecuteSynchronously);
                    break;
            }
        }

        public static void TryUnwrap<T>(this TaskCompletionSource<T> completionSource, Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                completionSource.TrySetException(aggregateException.InnerExceptions);
            }
            else
            {
                completionSource.TrySetException(exception);
            }
        }
        
        public static void TryUnwrap(this IPromise promise, Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                promise.TrySetException(aggregateException.InnerException);
            }
            else
            {
                promise.TrySetException(exception);
            }
        }

        public static Exception Unwrap(this Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                return aggregateException.InnerException;
            }

            return exception;
        }
    }
}