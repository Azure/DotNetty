// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     {@link AbstractBootstrap} is a helper class that makes it easy to bootstrap a {@link Channel}. It support
    ///     method-chaining to provide an easy way to configure the {@link AbstractBootstrap}.
    ///     <p>
    ///         When not used in a {@link ServerBootstrap} context, the {@link #bind()} methods are useful for connectionless
    ///         transports such as datagram (UDP).
    ///     </p>
    /// </summary>
    public abstract class AbstractBootstrap<TBootstrap, TChannel>
        where TBootstrap : AbstractBootstrap<TBootstrap, TChannel>
        where TChannel : IChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractBootstrap<TBootstrap, TChannel>>();

        volatile IEventLoopGroup group;
        volatile Func<TChannel> channelFactory;
        volatile EndPoint localAddress;
        readonly ConcurrentDictionary<ChannelOption, ChannelOptionValue> options;
        readonly ConcurrentDictionary<IConstant, AttributeValue> attrs;
        volatile IChannelHandler handler;

        protected internal AbstractBootstrap()
        {
            this.options = new ConcurrentDictionary<ChannelOption, ChannelOptionValue>();
            this.attrs = new ConcurrentDictionary<IConstant, AttributeValue>();
            // Disallow extending from a different package.
        }

        protected internal AbstractBootstrap(AbstractBootstrap<TBootstrap, TChannel> bootstrap)
        {
            this.group = bootstrap.group;
            this.channelFactory = bootstrap.channelFactory;
            this.handler = bootstrap.handler;
            this.localAddress = bootstrap.localAddress;
            this.options = new ConcurrentDictionary<ChannelOption, ChannelOptionValue>(bootstrap.options);
            this.attrs = new ConcurrentDictionary<IConstant, AttributeValue>(bootstrap.attrs);
        }

        /// <summary>
        ///     The {@link EventLoopGroup} which is used to handle all the events for the to-be-created
        ///     {@link Channel}
        /// </summary>
        public virtual TBootstrap Group(IEventLoopGroup group)
        {
            Contract.Requires(group != null);

            if (this.group != null)
            {
                throw new InvalidOperationException("group has already been set.");
            }
            this.group = group;
            return (TBootstrap)this;
        }

        /// <summary>
        ///     The {@link Class} which is used to create {@link Channel} instances from.
        ///     You either use this or {@link #channelFactory(io.netty.channel.ChannelFactory)} if your
        ///     {@link Channel} implementation has no no-args constructor.
        /// </summary>
        public TBootstrap Channel<T>()
            where T : TChannel, new() => this.ChannelFactory(() => new T());

        public TBootstrap ChannelFactory(Func<TChannel> channelFactory)
        {
            Contract.Requires(channelFactory != null);
            this.channelFactory = channelFactory;
            return (TBootstrap)this;
        }

        /// <summary>
        ///     The {@link SocketAddress} which is used to bind the local "end" to.
        /// </summary>
        public TBootstrap LocalAddress(EndPoint localAddress)
        {
            this.localAddress = localAddress;
            return (TBootstrap)this;
        }

        /// <summary>
        ///     @see {@link #localAddress(SocketAddress)}
        /// </summary>
        public TBootstrap LocalAddress(int inetPort) => this.LocalAddress(new IPEndPoint(IPAddress.Any, inetPort));

        /// <summary>
        ///     @see {@link #localAddress(SocketAddress)}
        /// </summary>
        public TBootstrap LocalAddress(string inetHost, int inetPort) => this.LocalAddress(new DnsEndPoint(inetHost, inetPort));

        /// <summary>
        ///     @see {@link #localAddress(SocketAddress)}
        /// </summary>
        public TBootstrap LocalAddress(IPAddress inetHost, int inetPort) => this.LocalAddress(new IPEndPoint(inetHost, inetPort));

        /// <summary>
        ///     Allow to specify a {@link ChannelOption} which is used for the {@link Channel} instances once they got
        ///     created. Use a value of {@code null} to remove a previous set {@link ChannelOption}.
        /// </summary>
        public TBootstrap Option<T>(ChannelOption<T> option, T value)
        {
            Contract.Requires(option != null);

            if (value == null)
            {
                ChannelOptionValue removed;
                this.options.TryRemove(option, out removed);
            }
            else
            {
                this.options[option] = new ChannelOptionValue<T>(option, value);
            }
            return (TBootstrap)this;
        }

        /// <summary>
        ///     Allow to specify an initial attribute of the newly created <see cref="IChannel" /> . If the <c>value</c> is
        ///     <c>null</c>, the attribute of the specified <c>key</c> is removed.
        /// </summary>
        public TBootstrap Attribute<T>(AttributeKey<T> key, T value)
            where T : class
        {
            Contract.Requires(key != null);

            if (value == null)
            {
                AttributeValue removed;
                this.attrs.TryRemove(key, out removed);
            }
            else
            {
                this.attrs[key] = new AttributeValue<T>(key, value);
            }
            return (TBootstrap)this;
        }

        /// <summary>
        ///     Allow to specify an initial attribute of the newly created {@link Channel}.  If the {@code value} is
        ///     {@code null}, the attribute of the specified {@code key} is removed.
        /// </summary>
        /// <summary>
        ///     Validate all the parameters. Sub-classes may override this, but should
        ///     call the super method in that case.
        /// </summary>
        public virtual TBootstrap Validate()
        {
            if (this.group == null)
            {
                throw new InvalidOperationException("group not set");
            }
            if (this.channelFactory == null)
            {
                throw new InvalidOperationException("channel or channelFactory not set");
            }
            return (TBootstrap)this;
        }

        /// <summary>
        ///     Returns a deep clone of this bootstrap which has the identical configuration.  This method is useful when making
        ///     multiple {@link Channel}s with similar settings.  Please note that this method does not clone the
        ///     {@link EventLoopGroup} deeply but shallowly, making the group a shared resource.
        /// </summary>
        public abstract TBootstrap Clone();

        /// <summary>
        ///     Create a new {@link Channel} and register it with an {@link EventLoop}.
        /// </summary>
        public Task RegisterAsync()
        {
            this.Validate();
            return this.InitAndRegisterAsync();
        }

        /// <summary>
        ///     Create a new {@link Channel} and bind it.
        /// </summary>
        public Task<IChannel> BindAsync()
        {
            this.Validate();
            EndPoint address = this.localAddress;
            if (address == null)
            {
                throw new InvalidOperationException("localAddress must be set beforehand.");
            }
            return this.DoBindAsync(address);
        }

        /// <summary>
        ///     Create a new {@link Channel} and bind it.
        /// </summary>
        public Task<IChannel> BindAsync(int inetPort) => this.BindAsync(new IPEndPoint(IPAddress.Any, inetPort));

        /// <summary>
        ///     Create a new {@link Channel} and bind it.
        /// </summary>
        public Task<IChannel> BindAsync(string inetHost, int inetPort) => this.BindAsync(new DnsEndPoint(inetHost, inetPort));

        /// <summary>
        ///     Create a new {@link Channel} and bind it.
        /// </summary>
        public Task<IChannel> BindAsync(IPAddress inetHost, int inetPort) => this.BindAsync(new IPEndPoint(inetHost, inetPort));

        /// <summary>
        ///     Create a new {@link Channel} and bind it.
        /// </summary>
        public Task<IChannel> BindAsync(EndPoint localAddress)
        {
            this.Validate();
            Contract.Requires(localAddress != null);

            return this.DoBindAsync(localAddress);
        }

        async Task<IChannel> DoBindAsync(EndPoint localAddress)
        {
            IChannel channel = await this.InitAndRegisterAsync();
            await DoBind0Async(channel, localAddress);

            return channel;
        }

        protected async Task<IChannel> InitAndRegisterAsync()
        {
            IChannel channel = this.channelFactory();
            try
            {
                this.Init(channel);
            }
            catch (Exception)
            {
                channel.Unsafe.CloseForcibly();
                // as the Channel is not registered yet we need to force the usage of the GlobalEventExecutor
                throw;
            }

            try
            {
                await this.Group().GetNext().RegisterAsync(channel);
            }
            catch (Exception)
            {
                if (channel.Registered)
                {
                    try
                    {
                        await channel.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                       Logger.Warn("Failed to close channel: " + channel, ex);
                    }
                }
                else
                {
                    channel.Unsafe.CloseForcibly();
                }
                throw;
            }

            // If we are here and the promise is not failed, it's one of the following cases:
            // 1) If we attempted registration from the event loop, the registration has been completed at this point.
            //    i.e. It's safe to attempt bind() or connect() now because the channel has been registered.
            // 2) If we attempted registration from the other thread, the registration request has been successfully
            //    added to the event loop's task queue for later execution.
            //    i.e. It's safe to attempt bind() or connect() now:
            //         because bind() or connect() will be executed *after* the scheduled registration task is executed
            //         because register(), bind(), and connect() are all bound to the same thread.

            return channel;
        }

        static Task DoBind0Async(IChannel channel, EndPoint localAddress)
        {
            // This method is invoked before channelRegistered() is triggered.  Give user handlers a chance to set up
            // the pipeline in its channelRegistered() implementation.
            var promise = new TaskCompletionSource();
            channel.EventLoop.Execute(() =>
            {
                try
                {
                    channel.BindAsync(localAddress).LinkOutcome(promise);
                }
                catch (Exception ex)
                {
                    channel.CloseSafe();
                    promise.TrySetException(ex);
                }
            });
            return promise.Task;
        }

        protected abstract void Init(IChannel channel);

        /// <summary>
        ///     the {@link ChannelHandler} to use for serving the requests.
        /// </summary>
        public TBootstrap Handler(IChannelHandler handler)
        {
            Contract.Requires(handler != null);
            this.handler = handler;
            return (TBootstrap)this;
        }

        protected EndPoint LocalAddress() => this.localAddress;

        protected IChannelHandler Handler() => this.handler;

        /// <summary>
        ///     Return the configured {@link EventLoopGroup} or {@code null} if non is configured yet.
        /// </summary>
        public IEventLoopGroup Group() => this.group;

        protected ICollection<ChannelOptionValue> Options => this.options.Values;

        protected ICollection<AttributeValue> Attributes => this.attrs.Values;

        protected static void SetChannelOptions(IChannel channel, ICollection<ChannelOptionValue> options, IInternalLogger logger)
        {
            foreach (var e in options)
            {
                SetChannelOption(channel, e, logger);
            }
        }

        protected static void SetChannelOptions(IChannel channel, ChannelOptionValue[] options, IInternalLogger logger)
        {
            foreach (var e in options)
            {
                SetChannelOption(channel, e, logger);
            }
        }

        protected static void SetChannelOption(IChannel channel, ChannelOptionValue option, IInternalLogger logger)
        {
            try
            {
                if (!option.Set(channel.Configuration))
                {
                    logger.Warn("Unknown channel option '{}' for channel '{}'", option.Option, channel);
                }
            }
            catch (Exception ex)
            {
                logger.Warn("Failed to set channel option '{}' with value '{}' for channel '{}'", option.Option, option, channel, ex);
            }
        }

        public override string ToString()
        {
            StringBuilder buf = new StringBuilder()
                .Append(this.GetType().Name)
                .Append('(');
            if (this.group != null)
            {
                buf.Append("group: ")
                    .Append(this.group.GetType().Name)
                    .Append(", ");
            }
            if (this.channelFactory != null)
            {
                buf.Append("channelFactory: ")
                    .Append(this.channelFactory)
                    .Append(", ");
            }
            if (this.localAddress != null)
            {
                buf.Append("localAddress: ")
                    .Append(this.localAddress)
                    .Append(", ");
            }

            if (this.options.Count > 0)
            {
                buf.Append("options: ")
                    .Append(this.options.ToDebugString())
                    .Append(", ");
            }

            if (this.attrs.Count > 0)
            {
                buf.Append("attrs: ")
                    .Append(this.attrs.ToDebugString())
                    .Append(", ");
            }

            if (this.handler != null)
            {
                buf.Append("handler: ")
                    .Append(this.handler)
                    .Append(", ");
            }
            if (buf[buf.Length - 1] == '(')
            {
                buf.Append(')');
            }
            else
            {
                buf[buf.Length - 2] = ')';
                buf.Length = buf.Length - 1;
            }
            return buf.ToString();
        }

        //static class PendingRegistrationPromise : DefaultChannelPromise
        //{
        //    // Is set to the correct EventExecutor once the registration was successful. Otherwise it will
        //    // stay null and so the GlobalEventExecutor.INSTANCE will be used for notifications.
        //    volatile EventExecutor executor;

        //    PendingRegistrationPromise(Channel channel)
        //    {
        //        super(channel);
        //    }

        //    protected EventExecutor executor()
        //    {
        //        EventExecutor executor = this.executor;
        //        if (executor != null)
        //        {
        //            // If the registration was a success executor is set.
        //            //
        //            // See https://github.com/netty/netty/issues/2586
        //            return executor;
        //        }
        //        // The registration failed so we can only use the GlobalEventExecutor as last resort to notify.
        //        return GlobalEventExecutor.INSTANCE;
        //    }
        //}

        protected abstract class ChannelOptionValue
        {
            public abstract ChannelOption Option { get; }
            public abstract bool Set(IChannelConfiguration config);
        }

        protected sealed class ChannelOptionValue<T> : ChannelOptionValue
        {
            public override ChannelOption Option { get; }
            readonly T value;

            public ChannelOptionValue(ChannelOption<T> option, T value)
            {
                this.Option = option;
                this.value = value;
            }

            public override bool Set(IChannelConfiguration config) => config.SetOption(this.Option, this.value);

            public override string ToString() => this.value.ToString();
        }

        protected abstract class AttributeValue
        {
            public abstract void Set(IAttributeMap map);
        }

        protected sealed class AttributeValue<T> : AttributeValue
            where T : class
        {
            readonly AttributeKey<T> key;
            readonly T value;

            public AttributeValue(AttributeKey<T> key, T value)
            {
                this.key = key;
                this.value = value;
            }

            public override void Set(IAttributeMap config) => config.GetAttribute(this.key).Set(this.value);
        }
    }
}