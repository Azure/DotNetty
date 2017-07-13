// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System;
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
    ///     A {@link Bootstrap} that makes it easy to bootstrap a {@link Channel} to use
    ///     for clients.
    ///     <p>
    ///         The {@link #bind()} methods are useful in combination with connectionless transports such as datagram (UDP).
    ///         For regular TCP connections, please use the provided {@link #connect()} methods.
    ///     </p>
    /// </summary>
    public class Bootstrap : AbstractBootstrap<Bootstrap, IChannel>
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Bootstrap>();

        static readonly INameResolver DefaultResolver = new DefaultNameResolver();

        volatile INameResolver resolver = DefaultResolver;
        volatile EndPoint remoteAddress;

        public Bootstrap()
        {
        }

        Bootstrap(Bootstrap bootstrap)
            : base(bootstrap)
        {
            this.resolver = bootstrap.resolver;
            this.remoteAddress = bootstrap.remoteAddress;
        }

        /// <summary>
        ///     Sets the {@link NameResolver} which will resolve the address of the unresolved named address.
        /// </summary>
        public Bootstrap Resolver(INameResolver resolver)
        {
            Contract.Requires(resolver != null);
            this.resolver = resolver;
            return this;
        }

        /// <summary>
        ///     The {@link SocketAddress} to connect to once the {@link #connect()} method
        ///     is called.
        /// </summary>
        public Bootstrap RemoteAddress(EndPoint remoteAddress)
        {
            this.remoteAddress = remoteAddress;
            return this;
        }

        /// <summary>
        ///     @see {@link #remoteAddress(SocketAddress)}
        /// </summary>
        public Bootstrap RemoteAddress(string inetHost, int inetPort)
        {
            this.remoteAddress = new DnsEndPoint(inetHost, inetPort);
            return this;
        }

        /// <summary>
        ///     @see {@link #remoteAddress(SocketAddress)}
        /// </summary>
        public Bootstrap RemoteAddress(IPAddress inetHost, int inetPort)
        {
            this.remoteAddress = new IPEndPoint(inetHost, inetPort);
            return this;
        }

        /// <summary>
        ///     Connect a {@link Channel} to the remote peer.
        /// </summary>
        public Task<IChannel> ConnectAsync()
        {
            this.Validate();
            EndPoint remoteAddress = this.remoteAddress;
            if (remoteAddress == null)
            {
                throw new InvalidOperationException("remoteAddress not set");
            }

            return this.DoResolveAndConnectAsync(remoteAddress, this.LocalAddress());
        }

        /// <summary>
        ///     Connect a {@link Channel} to the remote peer.
        /// </summary>
        public Task<IChannel> ConnectAsync(string inetHost, int inetPort) => this.ConnectAsync(new DnsEndPoint(inetHost, inetPort));

        /// <summary>
        ///     Connect a {@link Channel} to the remote peer.
        /// </summary>
        public Task<IChannel> ConnectAsync(IPAddress inetHost, int inetPort) => this.ConnectAsync(new IPEndPoint(inetHost, inetPort));

        /// <summary>
        ///     Connect a {@link Channel} to the remote peer.
        /// </summary>
        public Task<IChannel> ConnectAsync(EndPoint remoteAddress)
        {
            Contract.Requires(remoteAddress != null);

            this.Validate();
            return this.DoResolveAndConnectAsync(remoteAddress, this.LocalAddress());
        }

        /// <summary>
        ///     Connect a {@link Channel} to the remote peer.
        /// </summary>
        public Task<IChannel> ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            Contract.Requires(remoteAddress != null);

            this.Validate();
            return this.DoResolveAndConnectAsync(remoteAddress, localAddress);
        }

        /// <summary>
        ///     @see {@link #connect()}
        /// </summary>
        async Task<IChannel> DoResolveAndConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            IChannel channel = await this.InitAndRegisterAsync();

            if (this.resolver.IsResolved(remoteAddress))
            {
                // Resolver has no idea about what to do with the specified remote address or it's resolved already.
                await DoConnectAsync(channel, remoteAddress, localAddress);
                return channel;
            }

            EndPoint resolvedAddress;
            try
            {
                resolvedAddress = await this.resolver.ResolveAsync(remoteAddress);
            }
            catch (Exception)
            {
                try
                {
                    await channel.CloseAsync();
                }
                catch (Exception ex)
                {
                    Logger.Warn("Failed to close channel: " + channel, ex);
                }

                throw;
            }

            await DoConnectAsync(channel, resolvedAddress, localAddress);
            return channel;
        }

        static Task DoConnectAsync(IChannel channel,
            EndPoint remoteAddress, EndPoint localAddress)
        {
            // This method is invoked before channelRegistered() is triggered.  Give user handlers a chance to set up
            // the pipeline in its channelRegistered() implementation.
            var promise = new TaskCompletionSource();
            channel.EventLoop.Execute(() =>
            {
                try
                {
                    if (localAddress == null)
                    {
                        channel.ConnectAsync(remoteAddress).LinkOutcome(promise);
                    }
                    else
                    {
                        channel.ConnectAsync(remoteAddress, localAddress).LinkOutcome(promise);
                    }
                }
                catch (Exception ex)
                {
                    channel.CloseSafe();
                    promise.TrySetException(ex);
                }
            });
            return promise.Task;
        }

        protected override void Init(IChannel channel)
        {
            IChannelPipeline p = channel.Pipeline;
            p.AddLast(null, (string)null, this.Handler());

            ICollection<ChannelOptionValue> options = this.Options;
            SetChannelOptions(channel, options, Logger);

            ICollection<AttributeValue> attrs = this.Attributes;
            foreach (AttributeValue e in attrs)
            {
                e.Set(channel);
            }
        }

        public override Bootstrap Validate()
        {
            base.Validate();
            if (this.Handler() == null)
            {
                throw new InvalidOperationException("handler not set");
            }
            return this;
        }

        public override Bootstrap Clone() => new Bootstrap(this);

        /// <summary>
        ///     Returns a deep clone of this bootstrap which has the identical configuration except that it uses
        ///     the given {@link EventLoopGroup}. This method is useful when making multiple {@link Channel}s with similar
        ///     settings.
        /// </summary>
        public Bootstrap Clone(IEventLoopGroup group)
        {
            var bs = new Bootstrap(this);
            bs.Group(group);
            return bs;
        }

        public override string ToString()
        {
            if (this.remoteAddress == null)
            {
                return base.ToString();
            }

            var buf = new StringBuilder(base.ToString());
            buf.Length = buf.Length - 1;

            return buf.Append(", remoteAddress: ")
                .Append(this.remoteAddress)
                .Append(')')
                .ToString();
        }
    }
}