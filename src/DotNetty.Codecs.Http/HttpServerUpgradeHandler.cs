// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class HttpServerUpgradeHandler : HttpObjectAggregator
    {
        /// <summary>
        /// The source codec that is used in the pipeline initially.
        /// </summary>
        public interface ISourceCodec
        {
            /// <summary>
            /// Removes this codec (i.e. all associated handlers) from the pipeline.
            /// </summary>
            void UpgradeFrom(IChannelHandlerContext ctx);
        }

        /// <summary>
        /// A codec that the source can be upgraded to.
        /// </summary>
        public interface IUpgradeCodec
        {
            /// <summary>
            /// Gets all protocol-specific headers required by this protocol for a successful upgrade.
            /// Any supplied header will be required to appear in the {@link HttpHeaderNames#CONNECTION} header as well.
            /// </summary>
            ICollection<AsciiString> RequiredUpgradeHeaders { get; }

            /// <summary>
            /// Prepares the {@code upgradeHeaders} for a protocol update based upon the contents of {@code upgradeRequest}.
            /// This method returns a boolean value to proceed or abort the upgrade in progress. If {@code false} is
            /// returned, the upgrade is aborted and the {@code upgradeRequest} will be passed through the inbound pipeline
            /// as if no upgrade was performed. If {@code true} is returned, the upgrade will proceed to the next
            /// step which invokes {@link #upgradeTo}. When returning {@code true}, you can add headers to
            /// the {@code upgradeHeaders} so that they are added to the 101 Switching protocols response.
            /// </summary>
            bool PrepareUpgradeResponse(IChannelHandlerContext ctx, IFullHttpRequest upgradeRequest, HttpHeaders upgradeHeaders);

            /// <summary>
            /// Performs an HTTP protocol upgrade from the source codec. This method is responsible for
            /// adding all handlers required for the new protocol.
            ///
            /// ctx the context for the current handler.
            /// upgradeRequest the request that triggered the upgrade to this protocol.
            /// </summary>
            void UpgradeTo(IChannelHandlerContext ctx, IFullHttpRequest upgradeRequest);
        }

        /// <summary>
        ///  Creates a new UpgradeCodec for the requested protocol name.
        /// </summary>
        public interface IUpgradeCodecFactory
        {
            /// <summary>
            ///  Invoked by {@link HttpServerUpgradeHandler} for all the requested protocol names in the order of
            ///  the client preference.The first non-{@code null} {@link UpgradeCodec} returned by this method
            ///  will be selected.
            /// </summary>
            IUpgradeCodec NewUpgradeCodec(ICharSequence protocol);
        }

        public sealed class UpgradeEvent : IReferenceCounted
        {
            readonly ICharSequence protocol;
            readonly IFullHttpRequest upgradeRequest;

            internal UpgradeEvent(ICharSequence protocol, IFullHttpRequest upgradeRequest)
            {
                this.protocol = protocol;
                this.upgradeRequest = upgradeRequest;
            }

            public ICharSequence Protocol => this.protocol;

            public IFullHttpRequest UpgradeRequest => this.upgradeRequest;

            public int ReferenceCount => this.upgradeRequest.ReferenceCount;

            public IReferenceCounted Retain()
            {
                this.upgradeRequest.Retain();
                return this;
            }

            public IReferenceCounted Retain(int increment)
            {
                this.upgradeRequest.Retain(increment);
                return this;
            }

            public IReferenceCounted Touch()
            {
                this.upgradeRequest.Touch();
                return this;
            }

            public IReferenceCounted Touch(object hint)
            {
                this.upgradeRequest.Touch(hint);
                return this;
            }

            public bool Release() => this.upgradeRequest.Release();

            public bool Release(int decrement) => this.upgradeRequest.Release(decrement);

            public override string ToString() => $"UpgradeEvent [protocol={this.protocol}, upgradeRequest={this.upgradeRequest}]";
        }

        readonly ISourceCodec sourceCodec;
        readonly IUpgradeCodecFactory upgradeCodecFactory;
        bool handlingUpgrade;

        public HttpServerUpgradeHandler(ISourceCodec sourceCodec, IUpgradeCodecFactory upgradeCodecFactory)
            : this(sourceCodec, upgradeCodecFactory, 0)
        {
        }

        public HttpServerUpgradeHandler(ISourceCodec sourceCodec, IUpgradeCodecFactory upgradeCodecFactory, int maxContentLength) 
            : base(maxContentLength)
        {
            Contract.Requires(sourceCodec != null);
            Contract.Requires(upgradeCodecFactory != null);

            this.sourceCodec = sourceCodec;
            this.upgradeCodecFactory = upgradeCodecFactory;
        }

        protected override void Decode(IChannelHandlerContext context, IHttpObject message, List<object> output)
        {
            // Determine if we're already handling an upgrade request or just starting a new one.
            this.handlingUpgrade |= IsUpgradeRequest(message);
            if (!this.handlingUpgrade)
            {
                // Not handling an upgrade request, just pass it to the next handler.
                ReferenceCountUtil.Retain(message);
                output.Add(message);
                return;
            }

            if (message is IFullHttpRequest fullRequest)
            {
                ReferenceCountUtil.Retain(fullRequest);
                output.Add(fullRequest);
            }
            else
            {
                // Call the base class to handle the aggregation of the full request.
                base.Decode(context, message, output);
                if (output.Count == 0)
                {
                    // The full request hasn't been created yet, still awaiting more data.
                    return;
                }

                // Finished aggregating the full request, get it from the output list.
                Debug.Assert(output.Count == 1);
                this.handlingUpgrade = false;
                fullRequest = (IFullHttpRequest)output[0];
            }

            if (this.Upgrade(context, fullRequest))
            {
                // The upgrade was successful, remove the message from the output list
                // so that it's not propagated to the next handler. This request will
                // be propagated as a user event instead.
                output.Clear();
            }

            // The upgrade did not succeed, just allow the full request to propagate to the
            // next handler.
        }

        static bool IsUpgradeRequest(IHttpObject msg)
        {
            if (!(msg is IHttpRequest request))
            {
                return false;
            }
            return request.Headers.Contains(HttpHeaderNames.Upgrade);
        }

        bool Upgrade(IChannelHandlerContext ctx, IFullHttpRequest request)
        {
            // Select the best protocol based on those requested in the UPGRADE header.
            IList<ICharSequence> requestedProtocols = SplitHeader(request.Headers.Get(HttpHeaderNames.Upgrade, null));
            int numRequestedProtocols = requestedProtocols.Count;
            IUpgradeCodec upgradeCodec = null;
            ICharSequence upgradeProtocol = null;
            for (int i = 0; i < numRequestedProtocols; i++)
            {
                ICharSequence p = requestedProtocols[i];
                IUpgradeCodec c = this.upgradeCodecFactory.NewUpgradeCodec(p);
                if (c != null)
                {
                    upgradeProtocol = p;
                    upgradeCodec = c;
                    break;
                }
            }

            if (upgradeCodec == null)
            {
                // None of the requested protocols are supported, don't upgrade.
                return false;
            }

            // Make sure the CONNECTION header is present.
            ;
            if (!request.Headers.TryGet(HttpHeaderNames.Connection, out ICharSequence connectionHeader))
            {
                return false;
            }

            // Make sure the CONNECTION header contains UPGRADE as well as all protocol-specific headers.
            ICollection<AsciiString> requiredHeaders = upgradeCodec.RequiredUpgradeHeaders;
            IList<ICharSequence> values = SplitHeader(connectionHeader);
            if (!AsciiString.ContainsContentEqualsIgnoreCase(values, HttpHeaderNames.Upgrade) 
                || !AsciiString.ContainsAllContentEqualsIgnoreCase(values, requiredHeaders))
            {
                return false;
            }

            // Ensure that all required protocol-specific headers are found in the request.
            foreach (AsciiString requiredHeader in requiredHeaders)
            {
                if (!request.Headers.Contains(requiredHeader))
                {
                    return false;
                }
            }

            // Prepare and send the upgrade response. Wait for this write to complete before upgrading,
            // since we need the old codec in-place to properly encode the response.
            IFullHttpResponse upgradeResponse = CreateUpgradeResponse(upgradeProtocol);
            if (!upgradeCodec.PrepareUpgradeResponse(ctx, request, upgradeResponse.Headers))
            {
                return false;
            }

            // Create the user event to be fired once the upgrade completes.
            var upgradeEvent = new UpgradeEvent(upgradeProtocol, request);

            IUpgradeCodec finalUpgradeCodec = upgradeCodec;
            ctx.WriteAndFlushAsync(upgradeResponse).ContinueWith(t =>
                {
                    try
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            // Perform the upgrade to the new protocol.
                            this.sourceCodec.UpgradeFrom(ctx);
                            finalUpgradeCodec.UpgradeTo(ctx, request);

                            // Notify that the upgrade has occurred. Retain the event to offset
                            // the release() in the finally block.
                            ctx.FireUserEventTriggered(upgradeEvent.Retain());

                            // Remove this handler from the pipeline.
                            ctx.Channel.Pipeline.Remove(this);
                        }
                        else
                        {
                            ctx.Channel.CloseAsync();
                        }
                    }
                    finally
                    {
                        // Release the event if the upgrade event wasn't fired.
                        upgradeEvent.Release();
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
            return true;
        }

        static IFullHttpResponse CreateUpgradeResponse(ICharSequence upgradeProtocol)
        {
            var res = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.SwitchingProtocols, 
                Unpooled.Empty, false);
            res.Headers.Add(HttpHeaderNames.Connection, HttpHeaderValues.Upgrade);
            res.Headers.Add(HttpHeaderNames.Upgrade, upgradeProtocol);
            return res;
        }

        static IList<ICharSequence> SplitHeader(ICharSequence header)
        {
            var builder = new StringBuilder(header.Count);
            var protocols = new List<ICharSequence>(4);
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < header.Count; ++i)
            {
                char c = header[i];
                if (char.IsWhiteSpace(c))
                {
                    // Don't include any whitespace.
                    continue;
                }
                if (c == ',')
                {
                    // Add the string and reset the builder for the next protocol.
                    // Add the string and reset the builder for the next protocol.
                    protocols.Add(new AsciiString(builder.ToString()));
                    builder.Length = 0;
                }
                else
                {
                    builder.Append(c);
                }
            }

            // Add the last protocol
            if (builder.Length > 0)
            {
                protocols.Add(new AsciiString(builder.ToString()));
            }

            return protocols;
        }
    }
}
