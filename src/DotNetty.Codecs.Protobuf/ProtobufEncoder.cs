// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Protobuf
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Google.Protobuf;

    public class ProtobufEncoder : MessageToMessageEncoder<IMessage>
    {
        public override bool IsSharable => true;

        protected override void Encode(IChannelHandlerContext context, IMessage message, List<object> output)
        {
            Contract.Requires(context != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = null;

            try
            {
                int size = message.CalculateSize();
                if (size == 0)
                {
                    return;
                }
                //todo: Implement ByteBufferStream to avoid allocations.
                buffer = Unpooled.WrappedBuffer(message.ToByteArray());
                output.Add(buffer);
                buffer = null;
            }
            catch (Exception exception)
            {
                throw new CodecException(exception);
            }
            finally
            {
                buffer?.Release();
            }
        }
    }
}