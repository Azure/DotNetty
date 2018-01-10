// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.ProtocolBuffers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Google.ProtocolBuffers;

    public class ProtobufDecoder : MessageToMessageDecoder<IByteBuffer>
    {
        readonly IMessageLite protoType;
        readonly ExtensionRegistry extensionRegistry;

        public ProtobufDecoder(IMessageLite protoType, ExtensionRegistry extensionRegistry)
        {
            Contract.Requires(protoType != null);

            this.protoType = protoType.WeakDefaultInstanceForType;
            this.extensionRegistry = extensionRegistry;
        }

        public override bool IsSharable => true;

        protected override void Decode(IChannelHandlerContext context, IByteBuffer message, List<object> output)
        {
            Contract.Requires(context != null);
            Contract.Requires(message != null);
            Contract.Requires(output != null);

            int length = message.ReadableBytes;
            if (length <= 0)
            {
                return;
            }

            Stream inputStream = null;
            try
            {
                CodedInputStream codedInputStream;
                if (message.IoBufferCount == 1)
                {
                    ArraySegment<byte> bytes = message.GetIoBuffer(message.ReaderIndex, length);
                    codedInputStream = CodedInputStream.CreateInstance(bytes.Array, bytes.Offset, length);
                }
                else
                {
                    inputStream = new ReadOnlyByteBufferStream(message, false);
                    codedInputStream = CodedInputStream.CreateInstance(inputStream);
                }

                IBuilderLite newBuilder = this.protoType.WeakCreateBuilderForType();
                IBuilderLite messageBuilder = this.extensionRegistry == null
                    ? newBuilder.WeakMergeFrom(codedInputStream)
                    : newBuilder.WeakMergeFrom(codedInputStream, this.extensionRegistry);

                IMessageLite decodedMessage = messageBuilder.WeakBuild();
                if (decodedMessage != null)
                {
                    output.Add(decodedMessage);
                }
            }
            catch (Exception exception)
            {
                throw new CodecException(exception);
            }
            finally
            {
                inputStream?.Dispose();
            }
        }
    }
}