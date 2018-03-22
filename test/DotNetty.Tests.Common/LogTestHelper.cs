// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Extensions.Logging;

    public static class LogTestHelper
    {
        public static IDisposable SetInterceptionLogger(Intercept interceptAction)
        {
            InternalLoggerFactory.DefaultFactory.AddProvider(new InterceptionLoggerProvider(interceptAction));
            return new Disposable(() => InternalLoggerFactory.DefaultFactory.AddProvider(new EventSourceLoggerProvider()));
        }

        public delegate void Intercept(string categoryName, LogLevel logLevel, EventId eventId, string message, Exception exception);

        sealed class InterceptionLoggerProvider : ILoggerProvider
        {
            readonly Intercept interceptAction;

            public InterceptionLoggerProvider(Intercept interceptAction)
            {
                this.interceptAction = interceptAction;
            }

            public void Dispose()
            {
            }

            public ILogger CreateLogger(string categoryName) => new InterceptionLogger(categoryName, this.interceptAction);
        }

        sealed class InterceptionLogger : ILogger
        {
            readonly string categoryName;
            readonly Intercept interceptAction;

            public InterceptionLogger(string categoryName, Intercept interceptAction)
            {
                this.categoryName = categoryName;
                this.interceptAction = interceptAction;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                => this.interceptAction(this.categoryName, logLevel, eventId, formatter(state, exception), exception);

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state) => new Disposable(() => { });
        }
    }
}