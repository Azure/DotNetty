// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    using static DeflateDecoder;

    abstract class DeflateEncoder : WebSocketExtensionEncoder
    {
        readonly int compressionLevel;
        readonly int windowSize;
        readonly bool noContext;

        EmbeddedChannel encoder;

        protected DeflateEncoder(int compressionLevel, int windowSize, bool noContext)
        {
            this.compressionLevel = compressionLevel;
            this.windowSize = windowSize;
            this.noContext = noContext;
        }

        protected abstract int Rsv(WebSocketFrame msg);

        protected abstract bool RemoveFrameTail(WebSocketFrame msg);

        protected override void Encode(IChannelHandlerContext ctx, WebSocketFrame msg, List<object> output)
        {
            if (this.encoder == null)
            {
                this.encoder = new EmbeddedChannel(
                    ZlibCodecFactory.NewZlibEncoder(
                        ZlibWrapper.None,
                        this.compressionLevel,
                        this.windowSize,
                        8));
            }

            this.encoder.WriteOutbound(msg.Content.Retain());

            CompositeByteBuffer fullCompressedContent = ctx.Allocator.CompositeBuffer();
            for (;;)
            {
                var partCompressedContent = this.encoder.ReadOutbound<IByteBuffer>();
                if (partCompressedContent == null)
                {
                    break;
                }

                if (!partCompressedContent.IsReadable())
                {
                    partCompressedContent.Release();
                    continue;
                }

                fullCompressedContent.AddComponent(true, partCompressedContent);
            }

            if (fullCompressedContent.NumComponents <= 0)
            {
                fullCompressedContent.Release();
                throw new CodecException("cannot read compressed buffer");
            }

            if (msg.IsFinalFragment && this.noContext)
            {
                this.Cleanup();
            }

            IByteBuffer compressedContent;
            if (this.RemoveFrameTail(msg))
            {
                int realLength = fullCompressedContent.ReadableBytes - FrameTail.Length;
                compressedContent = fullCompressedContent.Slice(0, realLength);
            }
            else
            {
                compressedContent = fullCompressedContent;
            }

            WebSocketFrame outMsg;
            if (msg is TextWebSocketFrame)
            {
                outMsg = new TextWebSocketFrame(msg.IsFinalFragment, this.Rsv(msg), compressedContent);
            }
            else if (msg is BinaryWebSocketFrame)
            {
                outMsg = new BinaryWebSocketFrame(msg.IsFinalFragment, this.Rsv(msg), compressedContent);
            }
            else if (msg is ContinuationWebSocketFrame)
            {
                outMsg = new ContinuationWebSocketFrame(msg.IsFinalFragment, this.Rsv(msg), compressedContent);
            }
            else
            {
                throw new CodecException($"unexpected frame type: {msg.GetType().Name}");
            }

            output.Add(outMsg);
        }

        public override void HandlerRemoved(IChannelHandlerContext ctx)
        {
            this.Cleanup();
            base.HandlerRemoved(ctx);
        }

        void Cleanup()
        {
            if (this.encoder != null)
            {
                // Clean-up the previous encoder if not cleaned up correctly.
                if (this.encoder.Finish())
                {
                    for (;;)
                    {
                        var buf = this.encoder.ReadOutbound<IByteBuffer>();
                        if (buf == null)
                        {
                            break;
                        }
                        // Release the buffer
                        buf.Release();
                    }
                }
                this.encoder = null;
            }
        }
    }
}
