// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace HttpServer
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using System;
    using DotNetty.Common;

    sealed class HelloServerHandler : ChannelHandlerAdapter
    {
        static readonly ThreadLocalCache Cache = new ThreadLocalCache();

        sealed class ThreadLocalCache : FastThreadLocal<AsciiString>
        {
            protected override AsciiString GetInitialValue()
            {
                DateTime dateTime = DateTime.UtcNow;
                return AsciiString.Cached($"{dateTime.DayOfWeek}, {dateTime:dd MMM yyyy HH:mm:ss z}");
            }
        }

        static readonly byte[] StaticPlaintext = Encoding.UTF8.GetBytes("Hello, World!");
        static readonly int StaticPlaintextLen = StaticPlaintext.Length;
        static readonly IByteBuffer PlaintextContentBuffer = Unpooled.UnreleasableBuffer(Unpooled.DirectBuffer().WriteBytes(StaticPlaintext));
        static readonly AsciiString PlaintextClheaderValue = AsciiString.Cached($"{StaticPlaintextLen}");
        static readonly AsciiString JsonClheaderValue = AsciiString.Cached($"{JsonLen()}");

        static readonly AsciiString TypePlain = AsciiString.Cached("text/plain");
        static readonly AsciiString TypeJson = AsciiString.Cached("application/json");
        static readonly AsciiString ServerName = AsciiString.Cached("Netty");
        static readonly AsciiString ContentTypeEntity = HttpHeaderNames.ContentType;
        static readonly AsciiString DateEntity = HttpHeaderNames.Date;
        static readonly AsciiString ContentLengthEntity = HttpHeaderNames.ContentLength;
        static readonly AsciiString ServerEntity = HttpHeaderNames.Server;

        volatile ICharSequence date = Cache.Value;

        static int JsonLen() => Encoding.UTF8.GetBytes(NewMessage().ToJsonFormat()).Length;

        static MessageBody NewMessage() => new MessageBody("Hello, World!");

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            if (message is IHttpRequest request)
            {
                try
                {
                    this.Process(ctx, request);
                }
                finally
                {
                    ReferenceCountUtil.Release(message);
                }
            }
            else
            {
                ctx.FireChannelRead(message);
            }
        }

        void Process(IChannelHandlerContext ctx, IHttpRequest request)
        {
            string uri = request.Uri;
            switch (uri)
            {
                case "/plaintext":
                    this.WriteResponse(ctx, PlaintextContentBuffer.Duplicate(), TypePlain, PlaintextClheaderValue);
                    break;
                case "/json":
                    byte[] json = Encoding.UTF8.GetBytes(NewMessage().ToJsonFormat());
                    this.WriteResponse(ctx, Unpooled.WrappedBuffer(json), TypeJson, JsonClheaderValue);
                    break;
                default:
                    var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.NotFound, Unpooled.Empty, false);
                    ctx.WriteAndFlushAsync(response);
                    ctx.CloseAsync();
                    break;
            }
        }

        void WriteResponse(IChannelHandlerContext ctx, IByteBuffer buf, ICharSequence contentType, ICharSequence contentLength)
        {
            // Build the response object.
            var response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, buf, false);
            HttpHeaders headers = response.Headers;
            headers.Set(ContentTypeEntity, contentType);
            headers.Set(ServerEntity, ServerName);
            headers.Set(DateEntity, this.date);
            headers.Set(ContentLengthEntity, contentLength);

            // Close the non-keep-alive connection after the write operation is done.
            ctx.WriteAsync(response);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => context.CloseAsync();

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();
    }
}
