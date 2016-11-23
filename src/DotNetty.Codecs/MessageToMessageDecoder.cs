// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     Message to message decoder.
    /// </summary>
    public abstract class MessageToMessageDecoder<T> : ChannelHandlerAdapter
    {
        public virtual bool AcceptInboundMessage(object msg) => msg is T;

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();

            try
            {
                if (this.AcceptInboundMessage(message))
                {
                    var cast = (T)message;
                    try
                    {
                        this.Decode(context, cast, output);
                    }
                    finally
                    {
                        ReferenceCountUtil.Release(cast);
                    }
                }
                else
                {
                    output.Add(message);
                }
            }
            catch (DecoderException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new DecoderException(e);
            }
            finally
            {
                int size = output.Count;
                for (int i = 0; i < size; i++)
                {
                    context.FireChannelRead(output[i]);
                }
                output.Return();
            }
        }

        /// <summary>
        ///     Decode from one message to an other. This method will be called for each written message that can be handled
        ///     by this encoder.
        /// </summary>
        /// <param name="context">the {@link ChannelHandlerContext} which this {@link MessageToMessageDecoder} belongs to</param>
        /// <param name="message">the message to decode to an other one</param>
        /// <param name="output">the {@link List} to which decoded messages should be added</param>
        protected internal abstract void Decode(IChannelHandlerContext context, T message, List<object> output);
    }
}