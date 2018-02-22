// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class MessageToByteEncoder<T> : ChannelHandlerAdapter
    {
        public virtual bool AcceptOutboundMessage(object message) => message is T;

        public override ChannelFuture WriteAsync(IChannelHandlerContext context, object message)
        {
            Contract.Requires(context != null);

            IByteBuffer buffer = null;
            ChannelFuture result;
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
                        result = context.WriteAsync(buffer);
                    }
                    else
                    {
                        buffer.Release();
                        result = context.WriteAsync(Unpooled.Empty);
                    }

                    buffer = null;
                }
                else
                {
                    return context.WriteAsync(message);
                }
            }
            catch (EncoderException e)
            {
                throw;//return TaskEx.FromException(e);
            }
            catch (Exception ex)
            {
                throw new EncoderException(ex);//return TaskEx.FromException(new EncoderException(ex));
            }
            finally
            {
                buffer?.Release();
            }

            return result;
        }

        protected virtual IByteBuffer AllocateBuffer(IChannelHandlerContext context)
        {
            Contract.Requires(context != null);

            return context.Allocator.Buffer();
        }

        protected abstract void Encode(IChannelHandlerContext context, T message, IByteBuffer output);
    }
}
