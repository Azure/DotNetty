// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class Utf8FrameValidator : ChannelHandlerAdapter
    {
        int fragmentedFramesCount;
        Utf8Validator utf8Validator;

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            if (message is WebSocketFrame frame)
            {
                // Processing for possible fragmented messages for text and binary
                // frames
                if (frame.IsFinalFragment)
                {
                    // Final frame of the sequence. Apparently ping frames are
                    // allowed in the middle of a fragmented message
                    if (!(frame is PingWebSocketFrame))
                    {
                        this.fragmentedFramesCount = 0;

                        // Check text for UTF8 correctness
                        if (frame is TextWebSocketFrame 
                            || (this.utf8Validator != null && this.utf8Validator.IsChecking))
                        {
                            // Check UTF-8 correctness for this payload
                            this.CheckUtf8String(ctx, frame.Content);

                            // This does a second check to make sure UTF-8
                            // correctness for entire text message
                            this.utf8Validator.Finish();
                        }
                    }
                }
                else
                {
                    // Not final frame so we can expect more frames in the
                    // fragmented sequence
                    if (this.fragmentedFramesCount == 0)
                    {
                        // First text or binary frame for a fragmented set
                        if (frame is TextWebSocketFrame)
                        {
                            this.CheckUtf8String(ctx, frame.Content);
                        }
                    }
                    else
                    {
                        // Subsequent frames - only check if init frame is text
                        if (this.utf8Validator != null && this.utf8Validator.IsChecking)
                        {
                            this.CheckUtf8String(ctx, frame.Content);
                        }
                    }

                    // Increment counter
                    this.fragmentedFramesCount++;
                }
            }

            base.ChannelRead(ctx, message);
        }

        void CheckUtf8String(IChannelHandlerContext ctx, IByteBuffer buffer)
        {
            try
            {
                if (this.utf8Validator == null)
                {
                    this.utf8Validator = new Utf8Validator();
                }
                this.utf8Validator.Check(buffer);
            }
            catch (CorruptedFrameException)
            {
                if (ctx.Channel.Active)
                {
                    ctx.WriteAndFlushAsync(Unpooled.Empty)
                        .ContinueWith(t => ctx.Channel.CloseAsync());
                }
            }
        }
    }
}
