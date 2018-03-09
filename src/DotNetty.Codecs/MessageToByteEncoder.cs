// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class MessageToByteEncoder<T> : ChannelHandlerAdapter
    {
        public virtual bool AcceptOutboundMessage(object message) => message is T;

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            Contract.Requires(context != null);

            IByteBuffer buffer = null;
            try
            {
                if (this.AcceptOutboundMessage(message))
                {
                    buffer = this.AllocateBuffer(context);
                    var input = (T)message;
                    try
                    {
                        this.Encode(context, input, buffer);
                    }
                    finally
                    {
                        ReferenceCountUtil.Release(input);
                    }

                    if (buffer.IsReadable())
                    {
                        context.WriteAsync(buffer, promise);
                    }
                    else
                    {
                        buffer.Release();
                        context.WriteAsync(Unpooled.Empty, promise);
                    }

                    buffer = null;
                }
                else
                {
                    context.WriteAsync(message, promise);
                }
            }
            catch (EncoderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new EncoderException(ex);
            }
            finally
            {
                buffer?.Release();
            }
        }

        protected virtual IByteBuffer AllocateBuffer(IChannelHandlerContext context)
        {
            Contract.Requires(context != null);

            return context.Allocator.Buffer();
        }

        protected abstract void Encode(IChannelHandlerContext context, T message, IByteBuffer output);
    }
}
