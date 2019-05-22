// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public static class TaskEx
    {
        static Func<Task, Delegate> getTaskDelegate;
        static Action<TaskScheduler, Task> defaultQueueTask;

        public static readonly Task<int> Zero = Task.FromResult(0);

        public static readonly Task<int> Completed = Zero;

        public static readonly Task<int> Cancelled = CreateCancelledTask();

        public static readonly Task<bool> True = Task.FromResult(true);

        public static readonly Task<bool> False = Task.FromResult(false);

        static TaskEx()
        {
            GenerateGetTaskDelegate();
            GenerateDefaultQueueTask();
        }

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

        static readonly Action<Task, object> LinkOutcomeContinuationAction = (t, tcs) =>
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
                    task.ContinueWith(
                        LinkOutcomeContinuationAction,
                        taskCompletionSource,
                        TaskContinuationOptions.ExecuteSynchronously);
                    break;
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

        public static Exception Unwrap(this Exception exception)
        {
            if (exception is AggregateException aggregateException)
            {
                return aggregateException.InnerException;
            }

            return exception;
        }

        static void GenerateGetTaskDelegate()
        {
            ParameterExpression param = Expression.Parameter(typeof(Task), "task");
            MemberExpression filed = Expression.Field(param, "m_action");
            // netcore2.2.3 System.Private.CoreLib.dll m_action defined as Delegate
            // net461 mscorlib.dll m_action defined as object
            Expression<Func<Task, object>> lambda = Expression.Lambda<Func<Task, object>>(filed, param);
            Func<Task, object> func = lambda.Compile();
            getTaskDelegate = task =>
            {
                var dlt = func(task) as Delegate;
                if (dlt == null)
                {
                    throw new FieldAccessException("Task m_action isn't a Delegate, please check Task source code");
                }

                return dlt;
            };
        }

        static void GenerateDefaultQueueTask()
        {
            ParameterExpression instance = Expression.Parameter(typeof(TaskScheduler), "scheduler");
            ParameterExpression param = Expression.Parameter(typeof(Task), "task");
            MethodInfo methodInfo =
                typeof(TaskScheduler).GetMethod("QueueTask", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodCallExpression method = Expression.Call(instance, methodInfo, param);
            Expression<Action<TaskScheduler, Task>> lambda =
                Expression.Lambda<Action<TaskScheduler, Task>>(method, instance, param);
            defaultQueueTask = lambda.Compile();
        }

        internal static bool IsNettyTask(Task task)
        {
            MethodInfo methodInfo = getTaskDelegate(task).GetMethodInfo();
            return methodInfo.DeclaringType != null && methodInfo.DeclaringType.Namespace.StartsWith("DotNetty.");
        }

        internal static void DefaultQueueTask(Task task)
        {
            defaultQueueTask(TaskScheduler.Default, task);
        }
    }
}