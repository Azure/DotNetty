// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Base64
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public sealed class Base64Decoder : MessageToMessageDecoder<IByteBuffer>
    {
        readonly Base64Dialect dialect;

        public Base64Decoder()
            : this(Base64Dialect.STANDARD)
        {
        }

        public Base64Decoder(Base64Dialect dialect)
        {
            this.dialect = dialect;
        }

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer message, List<object> output) => output.Add(Base64.Decode(message, this.dialect));

        public override bool IsSharable => true;
    }
}