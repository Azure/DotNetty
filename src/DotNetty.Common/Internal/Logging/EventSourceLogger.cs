// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    using System;
    using Microsoft.Extensions.Logging;

    sealed class EventSourceLogger : ILogger
    {
        readonly string name;

        public EventSourceLogger(string name)
        {
            this.name = name;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    DefaultEventSource.Log.Trace(this.name, formatter(state, exception), exception);
                    break;
                case LogLevel.Debug:
                    DefaultEventSource.Log.Debug(this.name, formatter(state, exception), exception);
                    break;
                case LogLevel.Information:
                    DefaultEventSource.Log.Info(this.name, formatter(state, exception), exception);
                    break;
                case LogLevel.Warning:
                    DefaultEventSource.Log.Warning(this.name, formatter(state, exception), exception);
                    break;
                case LogLevel.Error:
                    DefaultEventSource.Log.Error(this.name, formatter(state, exception), exception);
                    break;
                case LogLevel.Critical:
                    DefaultEventSource.Log.Error(this.name, formatter(state, exception), exception);
                    break;
                case LogLevel.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return DefaultEventSource.Log.IsTraceEnabled;
                case LogLevel.Debug:
                    return DefaultEventSource.Log.IsDebugEnabled;
                case LogLevel.Information:
                    return DefaultEventSource.Log.IsInfoEnabled;
                case LogLevel.Warning:
                    return DefaultEventSource.Log.IsWarningEnabled;
                case LogLevel.Error:
                    return DefaultEventSource.Log.IsErrorEnabled;
                case LogLevel.Critical:
                    return DefaultEventSource.Log.IsErrorEnabled;
                case LogLevel.None:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }

        public IDisposable BeginScope<TState>(TState state) => NoOpDisposable.Instance;

        sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new NoOpDisposable();

            NoOpDisposable()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}