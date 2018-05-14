// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    public abstract class HttpContentEncoder : MessageToMessageCodec<IHttpRequest, IHttpObject>
    {
        enum State
        {
            PassThrough,
            AwaitHeaders,
            AwaitContent
        }

        static readonly AsciiString ZeroLengthHead = AsciiString.Cached("HEAD");
        static readonly AsciiString ZeroLengthConnect = AsciiString.Cached("CONNECT");
        static readonly int ContinueCode = HttpResponseStatus.Continue.Code;

        readonly Queue<ICharSequence> acceptEncodingQueue = new Queue<ICharSequence>();
        EmbeddedChannel encoder;
        State state = State.AwaitHeaders;

        public override bool AcceptOutboundMessage(object msg) => msg is IHttpContent || msg is IHttpResponse;

        protected override void Decode(IChannelHandlerContext ctx, IHttpRequest msg, List<object> output)
        {
            ICharSequence acceptedEncoding = msg.Headers.Get(HttpHeaderNames.AcceptEncoding, HttpContentDecoder.Identity);

            HttpMethod meth = msg.Method;
            if (ReferenceEquals(meth, HttpMethod.Head))
            {
                acceptedEncoding = ZeroLengthHead;
            }
            else if (ReferenceEquals(meth, HttpMethod.Connect))
            {
                acceptedEncoding = ZeroLengthConnect;
            }

            this.acceptEncodingQueue.Enqueue(acceptedEncoding);
            output.Add(ReferenceCountUtil.Retain(msg));
        }

        protected override void Encode(IChannelHandlerContext ctx, IHttpObject msg, List<object> output)
        {
            bool isFull = msg is IHttpResponse && msg is ILastHttpContent;
            switch (this.state)
            {
                case State.AwaitHeaders:
                {
                    EnsureHeaders(msg);
                    Debug.Assert(this.encoder == null);

                    var res = (IHttpResponse)msg;
                    int code = res.Status.Code;
                    ICharSequence acceptEncoding;
                    if (code == ContinueCode)
                    {
                        // We need to not poll the encoding when response with CONTINUE as another response will follow
                        // for the issued request. See https://github.com/netty/netty/issues/4079
                        acceptEncoding = null;
                    }
                    else
                    {
                        // Get the list of encodings accepted by the peer.
                        acceptEncoding = this.acceptEncodingQueue.Count > 0 ? this.acceptEncodingQueue.Dequeue() : null;
                        if (acceptEncoding == null)
                        {
                            throw new InvalidOperationException("cannot send more responses than requests");
                        }
                    }

                    //
                    // per rfc2616 4.3 Message Body
                    // All 1xx (informational), 204 (no content), and 304 (not modified) responses MUST NOT include a
                    // message-body. All other responses do include a message-body, although it MAY be of zero length.
                    //
                    // 9.4 HEAD
                    // The HEAD method is identical to GET except that the server MUST NOT return a message-body
                    // in the response.
                    //
                    // Also we should pass through HTTP/1.0 as transfer-encoding: chunked is not supported.
                    //
                    // See https://github.com/netty/netty/issues/5382
                    //
                    if (IsPassthru(res.ProtocolVersion, code, acceptEncoding))
                    {
                        if (isFull)
                        {
                            output.Add(ReferenceCountUtil.Retain(res));
                        }
                        else
                        {
                            output.Add(res);
                            // Pass through all following contents.
                            this.state = State.PassThrough;
                        }
                        break;
                    }

                    if (isFull)
                    {
                        // Pass through the full response with empty content and continue waiting for the the next resp.
                        if (!((IByteBufferHolder)res).Content.IsReadable())
                        {
                            output.Add(ReferenceCountUtil.Retain(res));
                            break;
                        }
                    }

                    // Prepare to encode the content.
                    Result result = this.BeginEncode(res, acceptEncoding);

                    // If unable to encode, pass through.
                    if (result == null)
                    {
                        if (isFull)
                        {
                            output.Add(ReferenceCountUtil.Retain(res));
                        }
                        else
                        {
                            output.Add(res);
                            // Pass through all following contents.
                            this.state = State.PassThrough;
                        }
                        break;
                    }

                    this.encoder = result.ContentEncoder;

                    // Encode the content and remove or replace the existing headers
                    // so that the message looks like a decoded message.
                    res.Headers.Set(HttpHeaderNames.ContentEncoding, result.TargetContentEncoding);

                    // Output the rewritten response.
                    if (isFull)
                    {
                        // Convert full message into unfull one.
                        var newRes = new DefaultHttpResponse(res.ProtocolVersion, res.Status);
                        newRes.Headers.Set(res.Headers);
                        output.Add(newRes);

                        EnsureContent(res);
                        this.EncodeFullResponse(newRes, (IHttpContent)res, output);
                        break;
                    }
                    else
                    {
                        // Make the response chunked to simplify content transformation.
                        res.Headers.Remove(HttpHeaderNames.ContentLength);
                        res.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

                        output.Add(res);
                        this.state = State.AwaitContent;

                        if (!(msg is IHttpContent))
                        {
                            // only break out the switch statement if we have not content to process
                            // See https://github.com/netty/netty/issues/2006
                            break;
                        }
                        // Fall through to encode the content
                        goto case State.AwaitContent;
                    }
                }
                case State.AwaitContent:
                {
                    EnsureContent(msg);
                    if (this.EncodeContent((IHttpContent)msg, output))
                    {
                        this.state = State.AwaitHeaders;
                    }
                    break;
                }
                case State.PassThrough:
                {
                    EnsureContent(msg);
                    output.Add(ReferenceCountUtil.Retain(msg));
                    // Passed through all following contents of the current response.
                    if (msg is ILastHttpContent)
                    {
                        this.state = State.AwaitHeaders;
                    }
                    break;
                }
            }
        }

        void EncodeFullResponse(IHttpResponse newRes, IHttpContent content, IList<object> output)
        {
            int existingMessages = output.Count;
            this.EncodeContent(content, output);

            if (HttpUtil.IsContentLengthSet(newRes))
            {
                // adjust the content-length header
                int messageSize = 0;
                for (int i = existingMessages; i < output.Count; i++)
                {
                    if (output[i] is IHttpContent httpContent)
                    {
                        messageSize += httpContent.Content.ReadableBytes;
                    }
                }
                HttpUtil.SetContentLength(newRes, messageSize);
            }
            else
            {
                newRes.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
            }
        }

        static bool IsPassthru(HttpVersion version, int code, ICharSequence httpMethod) =>
            code < 200 || code == 204 || code == 304
            || (ReferenceEquals(httpMethod, ZeroLengthHead) || ReferenceEquals(httpMethod, ZeroLengthConnect) && code == 200)
            || ReferenceEquals(version, HttpVersion.Http10);

        static void EnsureHeaders(IHttpObject msg)
        {
            if (!(msg is IHttpResponse))
            {
                throw new CodecException($"unexpected message type: {msg.GetType().Name} (expected: {StringUtil.SimpleClassName<IHttpResponse>()})");
            }
        }

        static void EnsureContent(IHttpObject msg)
        {
            if (!(msg is IHttpContent))
            {
                throw new CodecException($"unexpected message type: {msg.GetType().Name} (expected: {StringUtil.SimpleClassName<IHttpContent>()})");
            }
        }

        bool EncodeContent(IHttpContent c, IList<object> output)
        {
            IByteBuffer content = c.Content;

            this.Encode(content, output);

            if (c is ILastHttpContent last)
            {
                this.FinishEncode(output);

                // Generate an additional chunk if the decoder produced
                // the last product on closure,
                HttpHeaders headers = last.TrailingHeaders;
                if (headers.IsEmpty)
                {
                    output.Add(EmptyLastHttpContent.Default);
                }
                else
                {
                    output.Add(new ComposedLastHttpContent(headers));
                }
                return true;
            }
            return false;
        }

        protected abstract Result BeginEncode(IHttpResponse headers, ICharSequence acceptEncoding);

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            this.CleanupSafely(context);
            base.HandlerRemoved(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.CleanupSafely(context);
            base.ChannelInactive(context);
        }

        void Cleanup()
        {
            if (this.encoder != null)
            {
                // Clean-up the previous encoder if not cleaned up correctly.
                this.encoder.FinishAndReleaseAll();
                this.encoder = null;
            }
        }

        void CleanupSafely(IChannelHandlerContext ctx)
        {
            try
            {
                this.Cleanup();
            }
            catch (Exception cause)
            {
                // If cleanup throws any error we need to propagate it through the pipeline
                // so we don't fail to propagate pipeline events.
                ctx.FireExceptionCaught(cause);
            }
        }

        void Encode(IByteBuffer buf, IList<object> output)
        {
            // call retain here as it will call release after its written to the channel
            this.encoder.WriteOutbound(buf.Retain());
            this.FetchEncoderOutput(output);
        }

        void FinishEncode(IList<object> output)
        {
            if (this.encoder.Finish())
            {
                this.FetchEncoderOutput(output);
            }
            this.encoder = null;
        }

        void FetchEncoderOutput(ICollection<object> output)
        {
            for (;;)
            {
                var buf = this.encoder.ReadOutbound<IByteBuffer>();
                if (buf == null)
                {
                    break;
                }
                if (!buf.IsReadable())
                {
                    buf.Release();
                    continue;
                }
                output.Add(new DefaultHttpContent(buf));
            }
        }

        public sealed class Result
        {
            public Result(ICharSequence targetContentEncoding, EmbeddedChannel contentEncoder)
            {
                Contract.Requires(targetContentEncoding != null);
                Contract.Requires(contentEncoder != null);

                this.TargetContentEncoding = targetContentEncoding;
                this.ContentEncoder = contentEncoder;
            }

            public ICharSequence TargetContentEncoding { get; }

            public EmbeddedChannel ContentEncoder { get; }
        }
    }
}
