// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    abstract class DeflateDecoder : WebSocketExtensionDecoder
    {
        internal static readonly byte[] FrameTail = { 0x00, 0x00, 0xff, 0xff };

        readonly bool noContext;

        EmbeddedChannel decoder;

        protected DeflateDecoder(bool noContext)
        {
            this.noContext = noContext;
        }

        protected abstract bool AppendFrameTail(WebSocketFrame msg);

        protected abstract int NewRsv(WebSocketFrame msg);

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame msg, List<object> output)
        {
            if (this.decoder == null)
            {
                if (!(msg is TextWebSocketFrame) && !(msg is BinaryWebSocketFrame))
                {
                    throw new CodecException($"unexpected initial frame type: {msg.GetType().Name}");
                }

                this.decoder = new EmbeddedChannel(ZlibCodecFactory.NewZlibDecoder(ZlibWrapper.None));
            }

            bool readable = msg.Content.IsReadable();
            this.decoder.WriteInbound(msg.Content.Retain());
            if (this.AppendFrameTail(msg))
            {
                this.decoder.WriteInbound(Unpooled.WrappedBuffer(FrameTail));
            }

            CompositeByteBuffer compositeUncompressedContent = ctx.Allocator.CompositeDirectBuffer();
            for (;;)
            {
                var partUncompressedContent = this.decoder.ReadInbound<IByteBuffer>();
                if (partUncompressedContent == null)
                {
                    break;
                }

                if (!partUncompressedContent.IsReadable())
                {
                    partUncompressedContent.Release();
                    continue;
                }

                compositeUncompressedContent.AddComponent(true, partUncompressedContent);
            }

            // Correctly handle empty frames
            // See https://github.com/netty/netty/issues/4348
            if (readable && compositeUncompressedContent.NumComponents <= 0)
            {
                compositeUncompressedContent.Release();
                throw new CodecException("cannot read uncompressed buffer");
            }

            if (msg.IsFinalFragment && this.noContext)
            {
                this.Cleanup();
            }

            WebSocketFrame outMsg;
            if (msg is TextWebSocketFrame)
            {
                outMsg = new TextWebSocketFrame(msg.IsFinalFragment, this.NewRsv(msg), compositeUncompressedContent);
            }
            else if (msg is BinaryWebSocketFrame)
            {
                outMsg = new BinaryWebSocketFrame(msg.IsFinalFragment, this.NewRsv(msg), compositeUncompressedContent);
            }
            else if (msg is ContinuationWebSocketFrame)
            {
                outMsg = new ContinuationWebSocketFrame(msg.IsFinalFragment, this.NewRsv(msg), compositeUncompressedContent);
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

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            this.Cleanup();
            base.ChannelInactive(ctx);
        }

        void Cleanup()
        {
            if (this.decoder != null)
            {
                // Clean-up the previous encoder if not cleaned up correctly.
                if (this.decoder.Finish())
                {
                    for (;;)
                    {
                        var buf = this.decoder.ReadOutbound<IByteBuffer>();
                        if (buf == null)
                        {
                            break;
                        }
                        // Release the buffer
                        buf.Release();
                    }
                }
                this.decoder = null;
            }
        }
    }
}
