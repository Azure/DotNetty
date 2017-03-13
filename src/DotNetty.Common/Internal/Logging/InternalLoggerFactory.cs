// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Creates an <see cref="IInternalLogger" /> or changes the default factory
    ///     implementation. This factory allows you to choose what logging framework
    ///     DotNetty should use.  The default factory is own <see cref="LoggerFactory"/> with <see cref="EventSourceLoggerProvider" /> registered.
    ///     You can change it to your preferred logging framework before other DotNetty classes are loaded:
    ///     <pre>
    ///         <code>InternalLoggerFactory.DefaultFactory = new LoggerFactory();</code>
    ///     </pre>
    ///     Please note that the new default factory is effective only for the classes
    ///     which were loaded after the default factory is changed.  Therefore, <see cref="DefaultFactory"/> should be set as early
    ///     as possible and should not be called more than once.
    /// </summary>
    public static class InternalLoggerFactory
    {
        static ILoggerFactory defaultFactory;

        // todo: port: revisit
        //static InternalLoggerFactory()
        //{
        //    // Initiate some time-consuming background jobs here,
        //    // because this class is often initialized at the earliest time.
        //    try
        //    {
        //        Class.forName(ThreadLocalRandom.class.getName(), true, InternalLoggerFactory.class.getClassLoader());
        //    } catch (Exception ignored) {
        //        // Should not fail, but it does not harm to fail.
        //    }
        //}

        static ILoggerFactory NewDefaultFactory(string name)
        {
            var f = new LoggerFactory();
            f.AddProvider(new EventSourceLoggerProvider());
            f.CreateLogger(name).LogDebug("Using EventSource as the default logging framework");
            return f;
        }

        /// <summary>
        ///     Gets or sets the default factory.
        /// </summary>
        public static ILoggerFactory DefaultFactory
        {
            get
            {
                ILoggerFactory factory = Volatile.Read(ref defaultFactory);
                if (factory == null)
                {
                    factory = NewDefaultFactory(typeof(InternalLoggerFactory).FullName);
                    ILoggerFactory current = Interlocked.CompareExchange(ref defaultFactory, factory, null);
                    if (current != null)
                    {
                        return current;
                    }
                }
                return factory;
            }
            set
            {
                Contract.Requires(value != null);

                Volatile.Write(ref defaultFactory, value);
            }
        }

        /// <summary>
        ///     Creates a new logger instance with the name of the specified type.
        /// </summary>
        /// <typeparam name="T">type where logger is used</typeparam>
        /// <returns>logger instance</returns>
        public static IInternalLogger GetInstance<T>() => GetInstance(typeof(T));

        /// <summary>
        ///     Creates a new logger instance with the name of the specified type.
        /// </summary>
        /// <param name="type">type where logger is used</param>
        /// <returns>logger instance</returns>
        public static IInternalLogger GetInstance(Type type) => GetInstance(type.FullName);

        /// <summary>
        ///     Creates a new logger instance with the specified name.
        /// </summary>
        /// <param name="name">logger name</param>
        /// <returns>logger instance</returns>
        public static IInternalLogger GetInstance(string name) => new GenericLogger(name, DefaultFactory.CreateLogger(name));
    }
}