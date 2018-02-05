// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Protobuf
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Google.Protobuf;

    public class ProtobufDecoder : MessageToMessageDecoder<IByteBuffer>
    {
        readonly MessageParser messageParser;

        public ProtobufDecoder(MessageParser messageParser)
        {
            Contract.Requires(messageParser != null);

            this.messageParser = messageParser;
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
                    codedInputStream = new CodedInputStream(bytes.Array, bytes.Offset, length);
                }
                else
                {
                    inputStream = new ReadOnlyByteBufferStream(message, false);
                    codedInputStream = new CodedInputStream(inputStream);
                }

                //
                // Note that we do not dispose the input stream because there is no input stream attached. 
                // Ideally, it should be disposed. BUT if it is disposed, a null reference exception is 
                // thrown because CodedInputStream flag leaveOpen is set to false for direct byte array reads,
                // when it is disposed the input stream is null.
                // 
                // In this case it is ok because the CodedInputStream does not own the byte data.
                //
                IMessage decoded = this.messageParser.ParseFrom(codedInputStream);
                if (decoded != null)
                {
                    output.Add(decoded);
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