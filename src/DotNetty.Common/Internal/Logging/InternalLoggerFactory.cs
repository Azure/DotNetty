// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal.Logging
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    ///     Creates an <see cref="IInternalLogger"/> or changes the default factory
    ///     implementation.  This factory allows you to choose what logging framework
    ///     Netty should use.  The default factory is <see cref="EventSourceLoggerFactory"/>
    ///     You can change it to your preferred logging framework before other Netty classes are loaded:
    ///     <pre>
    ///         <code>InternalLoggerFactory.SetDefaultFactory(new MyLoggerFactory());</code>
    ///     </pre>
    ///     Please note that the new default factory is effective only for the classes
    ///     which were loaded after the default factory is changed.  Therefore,
    ///     {@link #SetDefaultFactory(InternalLoggerFactory)} should be called as early
    ///     as possible and shouldn't be called more than once.
    /// </summary>
    public abstract class InternalLoggerFactory
    {
        static InternalLoggerFactory defaultFactory;

        static InternalLoggerFactory()
        {
            // todo: port: revisit
            // Initiate some time-consuming background jobs here,
            // because this class is often initialized at the earliest time.
            //try {
            //    Class.forName(ThreadLocalRandom.class.getName(), true, InternalLoggerFactory.class.getClassLoader());
            //} catch (Exception ignored) {
            //    // Should not fail, but it does not harm to fail.
            //}
        }

        static InternalLoggerFactory NewDefaultFactory(string name)
        {
            InternalLoggerFactory f = new EventSourceLoggerFactory();
            f.NewInstance(name).Debug("Using EventSource as the default logging framework");
            return f;
        }

        /// <summary>
        /// Gets or sets the default factory. The initial default factory is <see cref="EventSourceLoggerFactory"/>
        /// </summary>
        public static InternalLoggerFactory DefaultFactory
        {
            get
            {
                InternalLoggerFactory factory = Volatile.Read(ref defaultFactory);
                if (factory == null)
                {
                    factory = NewDefaultFactory(typeof(InternalLoggerFactory).FullName);
                    InternalLoggerFactory current = Interlocked.CompareExchange(ref defaultFactory, factory, null);
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
        /// Creates a new logger instance with the name of the specified type.
        /// </summary>
        /// <typeparam name="T">type where logger is used</typeparam>
        /// <returns>logger instance</returns>
        public static IInternalLogger GetInstance<T>()
        {
            return GetInstance(typeof(T));
        }

        /// <summary>
        /// Creates a new logger instance with the name of the specified type.
        /// </summary>
        /// <param name="type">type where logger is used</param>
        /// <returns>logger instance</returns>
        public static IInternalLogger GetInstance(Type type)
        {
            return GetInstance(type.FullName);
        }

        /// <summary>
        /// Creates a new logger instance with the specified name.
        /// </summary>
        /// <param name="name">logger name</param>
        /// <returns>logger instance</returns>
        public static IInternalLogger GetInstance(string name)
        {
            return DefaultFactory.NewInstance(name);
        }
        /// <summary>
        /// Creates a new logger instance with the specified name.
        /// </summary>
        /// <param name="name">logger name</param>
        /// <returns>logger instance</returns>
        protected internal abstract IInternalLogger NewInstance(string name);
    }
}