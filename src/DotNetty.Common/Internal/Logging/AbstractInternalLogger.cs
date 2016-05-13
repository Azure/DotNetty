// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    using System;
    using System.Diagnostics.Contracts;

    /// <summary>
    ///     A skeletal implementation of {@link IInternalLogger}.  This class implements
    ///     all methods that have a { @link InternalLogLevel } parameter by default to call
    ///     specific logger methods such as {@link #Info(String)} or {@link #isInfoEnabled()}.
    /// </summary>
    public abstract class AbstractInternalLogger : IInternalLogger
    {
        static readonly string EXCEPTION_MESSAGE = "Unexpected exception:";

        /// <summary>
        ///     Creates a new instance.
        /// </summary>
        /// <param name="name"></param>
        protected AbstractInternalLogger(string name)
        {
            Contract.Requires(name != null);

            this.Name = name;
        }

        public string Name { get; }

        public bool IsEnabled(InternalLogLevel level)
        {
            switch (level)
            {
                case InternalLogLevel.TRACE:
                    return this.TraceEnabled;
                case InternalLogLevel.DEBUG:
                    return this.DebugEnabled;
                case InternalLogLevel.INFO:
                    return this.InfoEnabled;
                case InternalLogLevel.WARN:
                    return this.WarnEnabled;
                case InternalLogLevel.ERROR:
                    return this.ErrorEnabled;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public abstract bool TraceEnabled { get; }

        public abstract void Trace(string msg);

        public abstract void Trace(string format, object arg);

        public abstract void Trace(string format, object argA, object argB);

        public abstract void Trace(string format, params object[] arguments);

        public abstract void Trace(string msg, Exception t);

        public void Trace(Exception t) => this.Trace(EXCEPTION_MESSAGE, t);

        public abstract bool DebugEnabled { get; }

        public abstract void Debug(string msg);

        public abstract void Debug(string format, object arg);

        public abstract void Debug(string format, object argA, object argB);

        public abstract void Debug(string format, params object[] arguments);

        public abstract void Debug(string msg, Exception t);

        public void Debug(Exception t) => this.Debug(EXCEPTION_MESSAGE, t);

        public abstract bool InfoEnabled { get; }

        public abstract void Info(string msg);

        public abstract void Info(string format, object arg);

        public abstract void Info(string format, object argA, object argB);

        public abstract void Info(string format, params object[] arguments);

        public abstract void Info(string msg, Exception t);

        public void Info(Exception t) => this.Info(EXCEPTION_MESSAGE, t);

        public abstract bool WarnEnabled { get; }

        public abstract void Warn(string msg);

        public abstract void Warn(string format, object arg);

        public abstract void Warn(string format, params object[] arguments);

        public abstract void Warn(string format, object argA, object argB);

        public abstract void Warn(string msg, Exception t);

        public void Warn(Exception t) => this.Warn(EXCEPTION_MESSAGE, t);

        public abstract bool ErrorEnabled { get; }

        public abstract void Error(string msg);

        public abstract void Error(string format, object arg);

        public abstract void Error(string format, object argA, object argB);

        public abstract void Error(string format, params object[] arguments);

        public abstract void Error(string msg, Exception t);

        public void Error(Exception t) => this.Error(EXCEPTION_MESSAGE, t);

        public void Log(InternalLogLevel level, string msg, Exception cause)
        {
            switch (level)
            {
                case InternalLogLevel.TRACE:
                    this.Trace(msg, cause);
                    break;
                case InternalLogLevel.DEBUG:
                    this.Debug(msg, cause);
                    break;
                case InternalLogLevel.INFO:
                    this.Info(msg, cause);
                    break;
                case InternalLogLevel.WARN:
                    this.Warn(msg, cause);
                    break;
                case InternalLogLevel.ERROR:
                    this.Error(msg, cause);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Log(InternalLogLevel level, Exception cause)
        {
            switch (level)
            {
                case InternalLogLevel.TRACE:
                    this.Trace(cause);
                    break;
                case InternalLogLevel.DEBUG:
                    this.Debug(cause);
                    break;
                case InternalLogLevel.INFO:
                    this.Info(cause);
                    break;
                case InternalLogLevel.WARN:
                    this.Warn(cause);
                    break;
                case InternalLogLevel.ERROR:
                    this.Error(cause);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Log(InternalLogLevel level, string msg)
        {
            switch (level)
            {
                case InternalLogLevel.TRACE:
                    this.Trace(msg);
                    break;
                case InternalLogLevel.DEBUG:
                    this.Debug(msg);
                    break;
                case InternalLogLevel.INFO:
                    this.Info(msg);
                    break;
                case InternalLogLevel.WARN:
                    this.Warn(msg);
                    break;
                case InternalLogLevel.ERROR:
                    this.Error(msg);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Log(InternalLogLevel level, string format, object arg)
        {
            switch (level)
            {
                case InternalLogLevel.TRACE:
                    this.Trace(format, arg);
                    break;
                case InternalLogLevel.DEBUG:
                    this.Debug(format, arg);
                    break;
                case InternalLogLevel.INFO:
                    this.Info(format, arg);
                    break;
                case InternalLogLevel.WARN:
                    this.Warn(format, arg);
                    break;
                case InternalLogLevel.ERROR:
                    this.Error(format, arg);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Log(InternalLogLevel level, string format, object argA, object argB)
        {
            switch (level)
            {
                case InternalLogLevel.TRACE:
                    this.Trace(format, argA, argB);
                    break;
                case InternalLogLevel.DEBUG:
                    this.Debug(format, argA, argB);
                    break;
                case InternalLogLevel.INFO:
                    this.Info(format, argA, argB);
                    break;
                case InternalLogLevel.WARN:
                    this.Warn(format, argA, argB);
                    break;
                case InternalLogLevel.ERROR:
                    this.Error(format, argA, argB);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Log(InternalLogLevel level, string format, params object[] arguments)
        {
            switch (level)
            {
                case InternalLogLevel.TRACE:
                    this.Trace(format, arguments);
                    break;
                case InternalLogLevel.DEBUG:
                    this.Debug(format, arguments);
                    break;
                case InternalLogLevel.INFO:
                    this.Info(format, arguments);
                    break;
                case InternalLogLevel.WARN:
                    this.Warn(format, arguments);
                    break;
                case InternalLogLevel.ERROR:
                    this.Error(format, arguments);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override string ToString() => this.GetType().Name + '(' + this.Name + ')';
    }
}