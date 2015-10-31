// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    using System;

    sealed class EventSourceLogger : AbstractInternalLogger
    {
        public EventSourceLogger(string name)
            : base(name)
        {
        }

        /// <summary>
        ///     Is this logger instance enabled for the TRACE level?
        /// </summary>
        /// <value>true if this Logger is enabled for level TRACE, false otherwise.</value>
        public override bool TraceEnabled
        {
            get { return DefaultEventSource.Log.IsTraceEnabled; }
        }

        /// <summary>
        ///     Log a message object at level TRACE.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        public override void Trace(string msg)
        {
            DefaultEventSource.Log.Trace(this.Name, msg);
        }

        /// <summary>
        ///     Log a message at level TRACE according to the specified format and
        ///     argument.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level TRACE.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        public override void Trace(string format, object arg)
        {
            if (this.TraceEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, arg);
                DefaultEventSource.Log.Trace(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level TRACE according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level TRACE.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        public override void Trace(string format, object argA, object argB)
        {
            if (this.TraceEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, argA, argB);
                DefaultEventSource.Log.Trace(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level TRACE according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level TRACE.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        public override void Trace(string format, params object[] arguments)
        {
            if (this.TraceEnabled)
            {
                FormattingTuple ft = MessageFormatter.ArrayFormat(format, arguments);
                DefaultEventSource.Log.Trace(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log an exception at level TRACE with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        public override void Trace(string msg, Exception t)
        {
            DefaultEventSource.Log.Trace(this.Name, msg, t);
        }

        /// <summary>
        ///     Is this logger instance enabled for the DEBUG level?
        /// </summary>
        /// <value>true if this Logger is enabled for level DEBUG, false otherwise.</value>
        public override bool DebugEnabled
        {
            get { return DefaultEventSource.Log.IsDebugEnabled; }
        }

        /// <summary>
        ///     Log a message object at level DEBUG.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        public override void Debug(string msg)
        {
            DefaultEventSource.Log.Debug(this.Name, msg);
        }

        /// <summary>
        ///     Log a message at level DEBUG according to the specified format and
        ///     argument.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level DEBUG.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        public override void Debug(string format, object arg)
        {
            if (this.DebugEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, arg);
                DefaultEventSource.Log.Debug(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level DEBUG according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level DEBUG.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        public override void Debug(string format, object argA, object argB)
        {
            if (this.DebugEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, argA, argB);
                DefaultEventSource.Log.Debug(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level DEBUG according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level DEBUG.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        public override void Debug(string format, params object[] arguments)
        {
            if (this.DebugEnabled)
            {
                FormattingTuple ft = MessageFormatter.ArrayFormat(format, arguments);
                DefaultEventSource.Log.Debug(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log an exception at level DEBUG with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        public override void Debug(string msg, Exception t)
        {
            DefaultEventSource.Log.Debug(this.Name, msg, t);
        }

        /// <summary>
        ///     Is this logger instance enabled for the INFO level?
        /// </summary>
        /// <value>true if this Logger is enabled for level INFO, false otherwise.</value>
        public override bool InfoEnabled
        {
            get { return DefaultEventSource.Log.IsInfoEnabled; }
        }

        /// <summary>
        ///     Log a message object at level INFO.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        public override void Info(string msg)
        {
            DefaultEventSource.Log.Info(this.Name, msg);
        }

        /// <summary>
        ///     Log a message at level INFO according to the specified format and
        ///     argument.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level INFO.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        public override void Info(string format, object arg)
        {
            if (this.InfoEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, arg);
                DefaultEventSource.Log.Info(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level INFO according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level INFO.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        public override void Info(string format, object argA, object argB)
        {
            if (this.InfoEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, argA, argB);
                DefaultEventSource.Log.Info(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level INFO according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level INFO.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        public override void Info(string format, params object[] arguments)
        {
            if (this.InfoEnabled)
            {
                FormattingTuple ft = MessageFormatter.ArrayFormat(format, arguments);
                DefaultEventSource.Log.Info(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log an exception at level INFO with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        public override void Info(string msg, Exception t)
        {
            DefaultEventSource.Log.Info(this.Name, msg, t);
        }

        /// <summary>
        ///     Is this logger instance enabled for the WARN level?
        /// </summary>
        /// <value>true if this Logger is enabled for level WARN, false otherwise.</value>
        public override bool WarnEnabled
        {
            get { return DefaultEventSource.Log.IsWarningEnabled; }
        }

        /// <summary>
        ///     Log a message object at level WARN.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        public override void Warn(string msg)
        {
            DefaultEventSource.Log.Warning(this.Name, msg);
        }

        /// <summary>
        ///     Log a message at level WARN according to the specified format and
        ///     argument.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level WARN.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        public override void Warn(string format, object arg)
        {
            if (this.WarnEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, arg);
                DefaultEventSource.Log.Warning(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level WARN according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level WARN.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        public override void Warn(string format, object argA, object argB)
        {
            if (this.WarnEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, argA, argB);
                DefaultEventSource.Log.Warning(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level WARN according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level WARN.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        public override void Warn(string format, params object[] arguments)
        {
            if (this.WarnEnabled)
            {
                FormattingTuple ft = MessageFormatter.ArrayFormat(format, arguments);
                DefaultEventSource.Log.Warning(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log an exception at level WARN with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        public override void Warn(string msg, Exception t)
        {
            DefaultEventSource.Log.Warning(this.Name, msg, t);
        }

        /// <summary>
        ///     Is this logger instance enabled for the ERROR level?
        /// </summary>
        /// <value>true if this Logger is enabled for level ERROR, false otherwise.</value>
        public override bool ErrorEnabled
        {
            get { return DefaultEventSource.Log.IsErrorEnabled; }
        }

        /// <summary>
        ///     Log a message object at level ERROR.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        public override void Error(string msg)
        {
            DefaultEventSource.Log.Error(this.Name, msg);
        }

        /// <summary>
        ///     Log a message at level ERROR according to the specified format and
        ///     argument.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level ERROR.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        public override void Error(string format, object arg)
        {
            if (this.ErrorEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, arg);
                DefaultEventSource.Log.Error(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level ERROR according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level ERROR.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        public override void Error(string format, object argA, object argB)
        {
            if (this.ErrorEnabled)
            {
                FormattingTuple ft = MessageFormatter.Format(format, argA, argB);
                DefaultEventSource.Log.Error(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log a message at level ERROR according to the specified format and
        ///     arguments.
        ///     <p>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level ERROR.
        ///     </p>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        public override void Error(string format, params object[] arguments)
        {
            if (this.ErrorEnabled)
            {
                FormattingTuple ft = MessageFormatter.ArrayFormat(format, arguments);
                DefaultEventSource.Log.Error(this.Name, ft.Message, ft.Exception);
            }
        }

        /// <summary>
        ///     Log an exception at level ERROR with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        public override void Error(string msg, Exception t)
        {
            DefaultEventSource.Log.Error(this.Name, msg, t);
        }
    }
}