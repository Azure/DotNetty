// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net;
    using DotNetty.Buffers;

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
    }
}
