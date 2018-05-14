// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// A skeletal server-side <see cref="IChannel"/> implementation. A server-side <see cref="IChannel"/> does not
    /// allow the following operations: <see cref="IChannel.ConnectAsync(EndPoint)"/>,
    /// <see cref="IChannel.DisconnectAsync()"/>, <see cref="IChannel.WriteAsync(object)"/>,
    /// <see cref="IChannel.Flush()"/>.
    /// </summary>
    public abstract class AbstractServerChannel : AbstractChannel, IServerChannel
    {
        static readonly ChannelMetadata METADATA = new ChannelMetadata(false, 16);

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        protected AbstractServerChannel()
            : base(null)
        {
        }

        public override ChannelMetadata Metadata => METADATA;

        protected override EndPoint RemoteAddressInternal => null;

        protected override void DoDisconnect() => throw new NotSupportedException();

        protected override IChannelUnsafe NewUnsafe() => new DefaultServerUnsafe(this);

        protected override void DoWrite(ChannelOutboundBuffer buf) => throw new NotSupportedException();

        protected override object FilterOutboundMessage(object msg) => throw new NotSupportedException();

        class DefaultServerUnsafe : AbstractUnsafe
        {
            readonly Task err;

            public DefaultServerUnsafe(AbstractChannel channel)
                : base(channel)
            {
                this.err = TaskEx.FromException(new NotSupportedException());
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => this.err;
        }
    }
}