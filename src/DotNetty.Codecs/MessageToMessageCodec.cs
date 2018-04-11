// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public abstract class MessageToMessageCodec<TInbound, TOutbound> : ChannelDuplexHandler
    {
        readonly Encoder encoder;
        readonly Decoder decoder;

        sealed class Encoder : MessageToMessageEncoder<object>
        {
            readonly MessageToMessageCodec<TInbound, TOutbound> codec;

            public Encoder(MessageToMessageCodec<TInbound, TOutbound> codec)
            {
                this.codec = codec;
            }

            public override bool AcceptOutboundMessage(object msg) => this.codec.AcceptOutboundMessage(msg);

            protected internal override void Encode(IChannelHandlerContext context, object message, List<object> output) => this.codec.Encode(context, (TOutbound)message, output);
        }

        sealed class Decoder : MessageToMessageDecoder<object>
        {
            readonly MessageToMessageCodec<TInbound, TOutbound> codec;

            public Decoder(MessageToMessageCodec<TInbound, TOutbound> codec)
            {
                this.codec = codec;
            }

            public override bool AcceptInboundMessage(object msg) => this.codec.AcceptInboundMessage(msg);

            protected internal override void Decode(IChannelHandlerContext context, object message, List<object> output) => 
                this.codec.Decode(context, (TInbound)message, output);
        }

        protected MessageToMessageCodec()
        {
            this.encoder = new Encoder(this);
            this.decoder = new Decoder(this);
        }

        public sealed override void ChannelRead(IChannelHandlerContext context, object message) => 
            this.decoder.ChannelRead(context, message);

        public sealed override Task WriteAsync(IChannelHandlerContext context, object message) => 
            this.encoder.WriteAsync(context, message);

        public virtual bool AcceptInboundMessage(object msg) => msg is TInbound;

        public virtual bool AcceptOutboundMessage(object msg) => msg is TOutbound;

        protected abstract void Encode(IChannelHandlerContext ctx, TOutbound msg, List<object> output);

        protected abstract void Decode(IChannelHandlerContext ctx, TInbound msg, List<object> output);
    }
}
