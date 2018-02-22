// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class MessageToMessageEncoder<T> : ChannelHandlerAdapter
    {
        /// <summary>
        ///     Returns {@code true} if the given message should be handled. If {@code false} it will be passed to the next
        ///     {@link ChannelHandler} in the {@link ChannelPipeline}.
        /// </summary>
        public virtual bool AcceptOutboundMessage(object msg) => msg is T;

        public override ChannelFuture WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            ChannelFuture result;
            ThreadLocalObjectList output = null;
            try
            {
                if (this.AcceptOutboundMessage(msg))
                {
                    output = ThreadLocalObjectList.NewInstance();
                    var cast = (T)msg;
                    try
                    {
                        this.Encode(ctx, cast, output);
                    }
                    finally
                    {
                        ReferenceCountUtil.Release(cast);
                    }

                    if (output.Count == 0)
                    {
                        output.Return();
                        output = null;

                        throw new EncoderException(this.GetType().Name + " must produce at least one message.");
                    }
                }
                else
                {
                    return ctx.WriteAsync(msg);
                }
            }
            catch (EncoderException e)
            {
                throw;//return TaskEx.FromException(e);
            }
            catch (Exception ex)
            {
                throw new EncoderException(ex);//return TaskEx.FromException(new EncoderException(ex)); // todo: we don't have a stack on EncoderException but it's present on inner exception.
            }
            finally
            {
                if (output != null)
                {
                    int lastItemIndex = output.Count - 1;
                    if (lastItemIndex == 0)
                    {
                        result = ctx.WriteAsync(output[0]);
                    }
                    else if (lastItemIndex > 0)
                    {
                        for (int i = 0; i < lastItemIndex; i++)
                        {
                            // we don't care about output from these messages as failure while sending one of these messages will fail all messages up to the last message - which will be observed by the caller in Task result.
                            ctx.WriteAsync(output[i]); // todo: optimize: once IChannelHandlerContext allows, pass "not interested in task" flag
                        }
                        result = ctx.WriteAsync(output[lastItemIndex]);
                    }
                    else
                    {
                        // 0 items in output - must never get here
                        result = default(ChannelFuture);
                    }
                    output.Return();
                }
                else
                {
                    // output was reset during exception handling - must never get here
                    result = default(ChannelFuture);
                }
            }
            return result;
        }

        /// <summary>
        ///     Encode from one message to an other. This method will be called for each written message that can be handled
        ///     by this encoder.
        ///     @param context           the {@link ChannelHandlerContext} which this {@link MessageToMessageEncoder} belongs to
        ///     @param message           the message to encode to an other one
        ///     @param output           the {@link List} into which the encoded message should be added
        ///     needs to do some kind of aggragation
        ///     @throws Exception    is thrown if an error accour
        /// </summary>
        protected internal abstract void Encode(IChannelHandlerContext context, T message, List<object> output);
    }
}