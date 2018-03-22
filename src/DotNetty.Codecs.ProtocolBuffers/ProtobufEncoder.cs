// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.ProtocolBuffers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Google.ProtocolBuffers;

    public class ProtobufEncoder : MessageToMessageEncoder<IMessageLite>
    {
        public override bool IsSharable => true;

        protected override void Encode(IChannelHandlerContext context, IMessageLite message, List<object> output)
        {
            Contract.Requires(context != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            IByteBuffer buffer = null;
            try
            {
                int size = message.SerializedSize;
                if (size <= 0)
                {
                    return;
                }

                buffer = context.Allocator.Buffer(size);
                ArraySegment<byte> data = buffer.GetIoBuffer(buffer.WriterIndex, size);
                CodedOutputStream stream = CodedOutputStream.CreateInstance(data.Array, data.Offset, size);
                message.WriteTo(stream);
                buffer.SetWriterIndex(buffer.WriterIndex + size);

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