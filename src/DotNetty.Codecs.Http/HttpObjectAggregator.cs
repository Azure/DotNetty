// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class HttpObjectAggregator : MessageAggregator<IHttpObject, IHttpMessage, IHttpContent, IFullHttpMessage>
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<HttpObjectAggregator>();
        static readonly IFullHttpResponse Continue = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue, Unpooled.Empty);
        static readonly IFullHttpResponse ExpectationFailed = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.ExpectationFailed, Unpooled.Empty);
        static readonly IFullHttpResponse TooLargeClose = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.RequestEntityTooLarge, Unpooled.Empty);
        static readonly IFullHttpResponse TooLarge = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.RequestEntityTooLarge, Unpooled.Empty);

        static HttpObjectAggregator()
        {
            ExpectationFailed.Headers.Set(HttpHeaderNames.ContentLength, HttpHeaderValues.Zero);
            TooLarge.Headers.Set(HttpHeaderNames.ContentLength, HttpHeaderValues.Zero);

            TooLargeClose.Headers.Set(HttpHeaderNames.ContentLength, HttpHeaderValues.Zero);
            TooLargeClose.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
        }

        readonly bool closeOnExpectationFailed;

        public HttpObjectAggregator(int maxContentLength) 
            : this(maxContentLength, false)
        {
        }

        public HttpObjectAggregator(int maxContentLength, bool closeOnExpectationFailed) 
            : base(maxContentLength)
        {
            this.closeOnExpectationFailed = closeOnExpectationFailed;
        }

        protected override bool IsStartMessage(IHttpObject msg) => msg is IHttpMessage;

        protected override bool IsContentMessage(IHttpObject msg) => msg is IHttpContent;

        protected override bool IsLastContentMessage(IHttpContent msg) => msg is ILastHttpContent;

        protected override bool IsAggregated(IHttpObject msg) => msg is IFullHttpMessage;

        protected override bool IsContentLengthInvalid(IHttpMessage start, int maxContentLength)
        {
            try
            {
                return HttpUtil.GetContentLength(start, -1) > maxContentLength;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        static object ContinueResponse(IHttpMessage start, int maxContentLength, IChannelPipeline pipeline)
        {
            if (HttpUtil.IsUnsupportedExpectation(start))
            {
                // if the request contains an unsupported expectation, we return 417
                pipeline.FireUserEventTriggered(HttpExpectationFailedEvent.Default);
                return ExpectationFailed.RetainedDuplicate();
            }
            else if (HttpUtil.Is100ContinueExpected(start))
            {
                // if the request contains 100-continue but the content-length is too large, we return 413
                if (HttpUtil.GetContentLength(start, -1L) <= maxContentLength)
                {
                    return Continue.RetainedDuplicate();
                }
                pipeline.FireUserEventTriggered(HttpExpectationFailedEvent.Default);
                return TooLarge.RetainedDuplicate();
            }

            return null;
        }

        protected override object NewContinueResponse(IHttpMessage start, int maxContentLength, IChannelPipeline pipeline)
        {
            object response = ContinueResponse(start, maxContentLength, pipeline);
            // we're going to respond based on the request expectation so there's no
            // need to propagate the expectation further.
            if (response != null)
            {
                start.Headers.Remove(HttpHeaderNames.Expect);
            }
            return response;
        }

        protected override bool CloseAfterContinueResponse(object msg) => 
            this.closeOnExpectationFailed && this.IgnoreContentAfterContinueResponse(msg);

        protected override bool IgnoreContentAfterContinueResponse(object msg) => 
            msg is IHttpResponse response && response.Status.CodeClass.Equals(HttpStatusClass.ClientError);

        protected override IFullHttpMessage BeginAggregation(IHttpMessage start, IByteBuffer content)
        {
            Debug.Assert(!(start is IFullHttpMessage));

            HttpUtil.SetTransferEncodingChunked(start, false);

            if (start is IHttpRequest request)
            {
                return new AggregatedFullHttpRequest(request, content, null);
            }
            else if (start is IHttpResponse response)
            {
                return new AggregatedFullHttpResponse(response, content, null);
            }

            throw new CodecException($"Invalid type {StringUtil.SimpleClassName(start)} expecting {nameof(IHttpRequest)} or {nameof(IHttpResponse)}");
        }

        protected override void Aggregate(IFullHttpMessage aggregated, IHttpContent content)
        {
            if (content is ILastHttpContent httpContent)
            {
                // Merge trailing headers into the message.
                ((AggregatedFullHttpMessage)aggregated).TrailingHeaders = httpContent.TrailingHeaders;
            }
        }

        protected override void FinishAggregation(IFullHttpMessage aggregated)
        {
            // Set the 'Content-Length' header. If one isn't already set.
            // This is important as HEAD responses will use a 'Content-Length' header which
            // does not match the actual body, but the number of bytes that would be
            // transmitted if a GET would have been used.
            //
            // See rfc2616 14.13 Content-Length
            if (!HttpUtil.IsContentLengthSet(aggregated))
            {
                aggregated.Headers.Set(
                    HttpHeaderNames.ContentLength,
                    new AsciiString(aggregated.Content.ReadableBytes.ToString()));
            }
        }

        protected override void HandleOversizedMessage(IChannelHandlerContext ctx, IHttpMessage oversized)
        {
            if (oversized is IHttpRequest)
            {
                // send back a 413 and close the connection

                // If the client started to send data already, close because it's impossible to recover.
                // If keep-alive is off and 'Expect: 100-continue' is missing, no need to leave the connection open.
                if (oversized is IFullHttpMessage ||
                    !HttpUtil.Is100ContinueExpected(oversized) && !HttpUtil.IsKeepAlive(oversized))
                {
                    ctx.WriteAndFlushAsync(TooLargeClose.RetainedDuplicate()).ContinueWith((t, s) =>
                        {
                            if (t.IsFaulted)
                            {
                                Logger.Debug("Failed to send a 413 Request Entity Too Large.", t.Exception);
                            }
                            ((IChannelHandlerContext)s).CloseAsync();
                        }, 
                        ctx,
                        TaskContinuationOptions.ExecuteSynchronously);
                }
                else
                {
                    ctx.WriteAndFlushAsync(TooLarge.RetainedDuplicate()).ContinueWith((t, s) =>
                        {
                            if (t.IsFaulted)
                            {
                                Logger.Debug("Failed to send a 413 Request Entity Too Large.", t.Exception);
                                ((IChannelHandlerContext)s).CloseAsync();
                            }
                        },
                        ctx,
                        TaskContinuationOptions.ExecuteSynchronously);
                }
                // If an oversized request was handled properly and the connection is still alive
                // (i.e. rejected 100-continue). the decoder should prepare to handle a new message.
                var decoder = ctx.Channel.Pipeline.Get<HttpObjectDecoder>();
                decoder?.Reset();
            }
            else if (oversized is IHttpResponse)
            {
                ctx.CloseAsync();
                throw new TooLongFrameException($"Response entity too large: {oversized}");
            }
            else
            {
                throw new InvalidOperationException($"Invalid type {StringUtil.SimpleClassName(oversized)}, expecting {nameof(IHttpRequest)} or {nameof(IHttpResponse)}");
            }
        }

        abstract class AggregatedFullHttpMessage : IFullHttpMessage
        {
            protected readonly IHttpMessage Message;
            readonly IByteBuffer content;
            HttpHeaders trailingHeaders;

            protected AggregatedFullHttpMessage(IHttpMessage message, IByteBuffer content, HttpHeaders trailingHeaders)
            {
                this.Message = message;
                this.content = content;
                this.trailingHeaders = trailingHeaders;
            }

            public HttpHeaders TrailingHeaders
            {
                get
                {
                    HttpHeaders headers = this.trailingHeaders;
                    return headers ?? EmptyHttpHeaders.Default;
                }
                internal set => this.trailingHeaders = value;
            }

            public HttpVersion ProtocolVersion => this.Message.ProtocolVersion;

            public IHttpMessage SetProtocolVersion(HttpVersion version)
            {
                this.Message.SetProtocolVersion(version);
                return this;
            }

            public HttpHeaders Headers => this.Message.Headers;

            public DecoderResult Result
            {
                get => this.Message.Result;
                set => this.Message.Result = value;
            }

            public IByteBuffer Content => this.content;

            public int ReferenceCount => this.content.ReferenceCount;

            public IReferenceCounted Retain()
            {
                this.content.Retain();
                return this;
            }

            public IReferenceCounted Retain(int increment)
            {
                this.content.Retain(increment);
                return this;
            }

            public IReferenceCounted Touch()
            {
                this.content.Touch();
                return this;
            }

            public IReferenceCounted Touch(object hint)
            {
                this.content.Touch(hint);
                return this;
            }

            public bool Release() => this.content.Release();

            public bool Release(int decrement) => this.content.Release(decrement);

            public abstract IByteBufferHolder Copy();

            public abstract IByteBufferHolder Duplicate();

            public abstract IByteBufferHolder RetainedDuplicate();

            public abstract IByteBufferHolder Replace(IByteBuffer content);
        }

        sealed class AggregatedFullHttpRequest : AggregatedFullHttpMessage, IFullHttpRequest
        {
            internal AggregatedFullHttpRequest(IHttpRequest message, IByteBuffer content, HttpHeaders trailingHeaders)
                : base(message, content, trailingHeaders)
            {
            }

            public override IByteBufferHolder Copy() => this.Replace(this.Content.Copy());

            public override IByteBufferHolder Duplicate() => this.Replace(this.Content.Duplicate());

            public override IByteBufferHolder RetainedDuplicate() => this.Replace(this.Content.RetainedDuplicate());

            public override IByteBufferHolder Replace(IByteBuffer content)
            {
                var dup = new DefaultFullHttpRequest(this.ProtocolVersion, this.Method, this.Uri, content, 
                    this.Headers.Copy(), this.TrailingHeaders.Copy());
                dup.Result = this.Result;
                return dup;
            }

            public HttpMethod Method => ((IHttpRequest)this.Message).Method;

            public IHttpRequest SetMethod(HttpMethod method)
            {
                ((IHttpRequest)this.Message).SetMethod(method);
                return this;
            }

            public string Uri => ((IHttpRequest)this.Message).Uri;

            public IHttpRequest SetUri(string uri)
            {
                ((IHttpRequest)this.Message).SetUri(uri);
                return this;
            }

            public override string ToString() => HttpMessageUtil.AppendFullRequest(new StringBuilder(256), this).ToString();
        }

        sealed class AggregatedFullHttpResponse : AggregatedFullHttpMessage, IFullHttpResponse
        {
            public AggregatedFullHttpResponse(IHttpResponse message, IByteBuffer content, HttpHeaders trailingHeaders)
                : base(message, content, trailingHeaders)
            {
            }

            public override IByteBufferHolder Copy() => this.Replace(this.Content.Copy());

            public override IByteBufferHolder Duplicate() => this.Replace(this.Content.Duplicate());

            public override IByteBufferHolder RetainedDuplicate() => this.Replace(this.Content.RetainedDuplicate());

            public override IByteBufferHolder Replace(IByteBuffer content)
            {
                var dup = new DefaultFullHttpResponse(this.ProtocolVersion, this.Status, content, 
                    this.Headers.Copy(), this.TrailingHeaders.Copy());
                dup.Result = this.Result;
                return dup;
            }

            public HttpResponseStatus Status => ((IHttpResponse)this.Message).Status;

            public IHttpResponse SetStatus(HttpResponseStatus status)
            {
                ((IHttpResponse)this.Message).SetStatus(status);
                return this;
            }

            public override string ToString() => HttpMessageUtil.AppendFullResponse(new StringBuilder(256), this).ToString();
        }
    }
}
