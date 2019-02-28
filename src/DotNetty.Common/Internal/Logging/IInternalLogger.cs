// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    using System;

    /// <summary>
    ///     <em>Internal-use-only</em> logger used by DotNetty. <strong>DO NOT</strong>
    ///     access this class outside of DotNetty.
    /// </summary>
    public interface IInternalLogger
    {
        /// <summary>
        ///     Return the name of this <see cref="IInternalLogger" /> instance.
        /// </summary>
        /// <value>name of this logger instance</value>
        string Name { get; }

        /// <summary>
        ///     Is this logger instance enabled for the TRACE level?
        /// </summary>
        /// <value>true if this Logger is enabled for level TRACE, false otherwise.</value>
        bool TraceEnabled { get; }

        /// <summary>
        ///     Log a message object at level TRACE.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        void Trace(string msg);

        /// <summary>
        ///     Log a message at level TRACE according to the specified format and
        ///     argument.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level TRACE.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        void Trace(string format, object arg);

        /// <summary>
        ///     Log a message at level TRACE according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level TRACE.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        void Trace(string format, object argA, object argB);

        /// <summary>
        ///     Log a message at level TRACE according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level TRACE. However, this variant incurs the hidden
        ///         (and relatively small) cost of creating an <c>object[]</c>
        ///         before invoking the method,
        ///         even if this logger is disabled for TRACE. The variants
        ///         <see cref="Trace(string, object)" /> and <see cref="Trace(string, object, object)" />
        ///         arguments exist solely to avoid this hidden cost.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        void Trace(string format, params object[] arguments);

        /// <summary>
        ///     Log an exception at level TRACE with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        void Trace(string msg, Exception t);

        /// <summary>
        ///     Log an exception at level TRACE.
        /// </summary>
        /// <param name="t">the exception to log</param>
        void Trace(Exception t);

        /// <summary>
        ///     Is this logger instance enabled for the DEBUG level?
        /// </summary>
        /// <value>true if this Logger is enabled for level DEBUG, false otherwise.</value>
        bool DebugEnabled { get; }

        /// <summary>
        ///     Log a message object at level DEBUG.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        void Debug(string msg);

        /// <summary>
        ///     Log a message at level DEBUG according to the specified format and
        ///     argument.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level DEBUG.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        void Debug(string format, object arg);

        /// <summary>
        ///     Log a message at level DEBUG according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level DEBUG.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        void Debug(string format, object argA, object argB);

        /// <summary>
        ///     Log a message at level DEBUG according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level DEBUG. However, this variant incurs the hidden
        ///         (and relatively small) cost of creating an <c>object[]</c>
        ///         before invoking the method,
        ///         even if this logger is disabled for DEBUG. The variants
        ///         <see cref="Debug(string, object)" /> and <see cref="Debug(string, object, object)" />
        ///         arguments exist solely to avoid this hidden cost.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        void Debug(string format, params object[] arguments);

        /// <summary>
        ///     Log an exception at level DEBUG with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        void Debug(string msg, Exception t);

        /// <summary>
        ///     Log an exception at level DEBUG.
        /// </summary>
        /// <param name="t">the exception to log</param>
        void Debug(Exception t);

        /// <summary>
        ///     Is this logger instance enabled for the INFO level?
        /// </summary>
        /// <value>true if this Logger is enabled for level INFO, false otherwise.</value>
        bool InfoEnabled { get; }

        /// <summary>
        ///     Log a message object at level INFO.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        void Info(string msg);

        /// <summary>
        ///     Log a message at level INFO according to the specified format and
        ///     argument.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level INFO.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        void Info(string format, object arg);

        /// <summary>
        ///     Log a message at level INFO according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level INFO.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        void Info(string format, object argA, object argB);

        /// <summary>
        ///     Log a message at level INFO according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level INFO. However, this variant incurs the hidden
        ///         (and relatively small) cost of creating an <c>object[]</c>
        ///         before invoking the method,
        ///         even if this logger is disabled for INFO. The variants
        ///         <see cref="Info(string, object)" /> and <see cref="Info(string, object, object)" />
        ///         arguments exist solely to avoid this hidden cost.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        void Info(string format, params object[] arguments);

        /// <summary>
        ///     Log an exception at level INFO with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        void Info(string msg, Exception t);

        /// <summary>
        ///     Log an exception at level INFO.
        /// </summary>
        /// <param name="t">the exception to log</param>
        void Info(Exception t);

        /// <summary>
        ///     Is this logger instance enabled for the WARN level?
        /// </summary>
        /// <value>true if this Logger is enabled for level WARN, false otherwise.</value>
        bool WarnEnabled { get; }

        /// <summary>
        ///     Log a message object at level WARN.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        void Warn(string msg);

        /// <summary>
        ///     Log a message at level WARN according to the specified format and
        ///     argument.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level WARN.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        void Warn(string format, object arg);

        /// <summary>
        ///     Log a message at level WARN according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level WARN. However, this variant incurs the hidden
        ///         (and relatively small) cost of creating an <c>object[]</c>
        ///         before invoking the method,
        ///         even if this logger is disabled for WARN. The variants
        ///         <see cref="Warn(string, object)" /> and <see cref="Warn(string, object, object)" />
        ///         arguments exist solely to avoid this hidden cost.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        void Warn(string format, params object[] arguments);

        /// <summary>
        ///     Log a message at level WARN according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level WARN.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        void Warn(string format, object argA, object argB);

        /// <summary>
        ///     Log an exception at level WARN with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        void Warn(string msg, Exception t);

        /// <summary>
        ///     Log an exception at level WARN.
        /// </summary>
        /// <param name="t">the exception to log</param>
        void Warn(Exception t);

        /// <summary>
        ///     Is this logger instance enabled for the ERROR level?
        /// </summary>
        /// <value>true if this Logger is enabled for level ERROR, false otherwise.</value>
        bool ErrorEnabled { get; }

        /// <summary>
        ///     Log a message object at level ERROR.
        /// </summary>
        /// <param name="msg">the message object to be logged</param>
        void Error(string msg);

        /// <summary>
        ///     Log a message at level ERROR according to the specified format and
        ///     argument.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level ERROR.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        void Error(string format, object arg);

        /// <summary>
        ///     Log a message at level ERROR according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level ERROR.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        void Error(string format, object argA, object argB);

        /// <summary>
        ///     Log a message at level ERROR according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for level ERROR. However, this variant incurs the hidden
        ///         (and relatively small) cost of creating an <c>object[]</c>
        ///         before invoking the method,
        ///         even if this logger is disabled for ERROR. The variants
        ///         <see cref="Error(string, object)" /> and <see cref="Error(string, object, object)" />
        ///         arguments exist solely to avoid this hidden cost.
        ///     </para>
        /// </summary>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        void Error(string format, params object[] arguments);

        /// <summary>
        ///     Log an exception at level ERROR with an accompanying message.
        /// </summary>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        void Error(string msg, Exception t);

        /// <summary>
        ///     Log an exception at level ERROR.
        /// </summary>
        /// <param name="t">the exception to log</param>
        void Error(Exception t);

        /// <summary>
        ///     Is the logger instance enabled for the specified <paramref name="level"/>?
        /// </summary>
        /// <param name="level">log level</param>
        /// <returns>true if this Logger is enabled for the specified <paramref name="level"/>, false otherwise.</returns>
        bool IsEnabled(InternalLogLevel level);

        /// <summary>
        ///     Log a message object at a specified <see cref="InternalLogLevel" />.
        /// </summary>
        /// <param name="level">log level</param>
        /// <param name="msg">the message object to be logged</param>
        void Log(InternalLogLevel level, string msg);

        /// <summary>
        ///     Log a message at a specified <see cref="InternalLogLevel" /> according to the specified format and
        ///     argument.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for the specified <see cref="InternalLogLevel" />.
        ///     </para>
        /// </summary>
        /// <param name="level">log level</param>
        /// <param name="format">the format string</param>
        /// <param name="arg">the argument</param>
        void Log(InternalLogLevel level, string format, object arg);

        /// <summary>
        ///     Log a message at a specified <see cref="InternalLogLevel" /> according to the specified format and
        ///     arguments.
        ///     <para>
        ///         This form avoids superfluous object creation when the logger is disabled
        ///         for the specified <see cref="InternalLogLevel" />.
        ///     </para>
        /// </summary>
        /// <param name="level">log level</param>
        /// <param name="format">the format string</param>
        /// <param name="argA">the first argument</param>
        /// <param name="argB">the second argument</param>
        void Log(InternalLogLevel level, string format, object argA, object argB);

        /// <summary>
        ///     Log a message at the specified <paramref name="level"/> according to the specified format
        ///     and arguments.
        ///     <para>
        ///         This form avoids superfluous string concatenation when the logger
        ///         is disabled for the specified <paramref name="level"/>. However, this variant incurs the hidden
        ///         (and relatively small) cost of creating an <c>object[]</c> before invoking the method,
        ///         even if this logger is disabled for the specified <paramref name="level"/>. The variants
        ///         <see cref="Log(InternalLogLevel, string, object)" /> and
        ///         <see cref="Log(InternalLogLevel, string, object, object)" /> arguments exist solely
        ///         in order to avoid this hidden cost.
        ///     </para>
        /// </summary>
        /// <param name="level">log level</param>
        /// <param name="format">the format string</param>
        /// <param name="arguments">an array of arguments</param>
        void Log(InternalLogLevel level, string format, params object[] arguments);

        /// <summary>
        ///     Log an exception at the specified <see cref="InternalLogLevel" /> with an
        ///     accompanying message.
        /// </summary>
        /// <param name="level">log level</param>
        /// <param name="msg">the message accompanying the exception</param>
        /// <param name="t">the exception to log</param>
        void Log(InternalLogLevel level, string msg, Exception t);

        /// <summary>
        ///     Log an exception at the specified <see cref="InternalLogLevel" />.
        /// </summary>
        /// <param name="level">log level</param>
        /// <param name="t">the exception to log</param>
        void Log(InternalLogLevel level, Exception t);
    }
}