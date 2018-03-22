// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Common;

    public sealed class DatagramPacket : DefaultAddressedEnvelope<IByteBuffer>, IByteBufferHolder
    {
        public DatagramPacket(IByteBuffer message, EndPoint recipient)
            : base(message, recipient)
        {
        }

        public DatagramPacket(IByteBuffer message, EndPoint sender, EndPoint recipient)
            : base(message, sender, recipient)
        {
        }

        public IByteBufferHolder Copy() => new DatagramPacket(this.Content.Copy(), this.Sender, this.Recipient);

        public IByteBufferHolder Duplicate() => new DatagramPacket(this.Content.Duplicate(), this.Sender, this.Recipient);

        public IByteBufferHolder RetainedDuplicate() => this.Replace(this.Content.RetainedDuplicate());

        public IByteBufferHolder Replace(IByteBuffer content) => new DatagramPacket(content, this.Recipient, this.Sender);

        public override IReferenceCounted Retain()
        {
            base.Retain();
            return this;
        }

        public override IReferenceCounted Retain(int increment)
        {
            base.Retain(increment);
            return this;
        }

        public override IReferenceCounted Touch()
        {
            base.Touch();
            return this;
        }

        public override IReferenceCounted Touch(object hint)
        {
            base.Touch(hint);
            return this;
        }
    }
}