// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    // Note HttpObjectAggregator already implements IChannelHandler
    public class HttpClientUpgradeHandler : HttpObjectAggregator
    {
        // User events that are fired to notify about upgrade status.
        public enum UpgradeEvent
        {
            // The Upgrade request was sent to the server.
            UpgradeIssued,

            // The Upgrade to the new protocol was successful.
            UpgradeSuccessful,

            // The Upgrade was unsuccessful due to the server not issuing
            // with a 101 Switching Protocols response.
            UpgradeRejected
        }

        public interface ISourceCodec
        {
            // Removes or disables the encoder of this codec so that the {@link UpgradeCodec} can send an initial greeting
            // (if any).
            void PrepareUpgradeFrom(IChannelHandlerContext ctx);

            // Removes this codec (i.e. all associated handlers) from the pipeline.
            void UpgradeFrom(IChannelHandlerContext ctx);
        }

        public interface IUpgradeCodec
        {
            // Returns the name of the protocol supported by this codec, as indicated by the {@code 'UPGRADE'} header.
            ICharSequence Protocol { get; }

            // Sets any protocol-specific headers required to the upgrade request. Returns the names of
            // all headers that were added. These headers will be used to populate the CONNECTION header.
            ICollection<ICharSequence> SetUpgradeHeaders(IChannelHandlerContext ctx, IHttpRequest upgradeRequest);

            ///
            // Performs an HTTP protocol upgrade from the source codec. This method is responsible for
            // adding all handlers required for the new protocol.
            // 
            // ctx the context for the current handler.
            // upgradeResponse the 101 Switching Protocols response that indicates that the server
            //            has switched to this protocol.
            void UpgradeTo(IChannelHandlerContext ctx, IFullHttpResponse upgradeResponse);
        }

        readonly ISourceCodec sourceCodec;
        readonly IUpgradeCodec upgradeCodec;
        bool upgradeRequested;

        public HttpClientUpgradeHandler(ISourceCodec sourceCodec, IUpgradeCodec upgradeCodec, int maxContentLength)
            : base(maxContentLength)
        {
            Contract.Requires(sourceCodec != null);
            Contract.Requires(upgradeCodec != null);

            this.sourceCodec = sourceCodec;
            this.upgradeCodec = upgradeCodec;
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            if (!(message is IHttpRequest))
            {
                return context.WriteAsync(message);
            }

            if (this.upgradeRequested)
            {
                return TaskEx.FromException(new InvalidOperationException("Attempting to write HTTP request with upgrade in progress"));
            }

            this.upgradeRequested = true;
            this.SetUpgradeRequestHeaders(context, (IHttpRequest)message);

            // Continue writing the request.
            Task task = context.WriteAsync(message);

            // Notify that the upgrade request was issued.
            context.FireUserEventTriggered(UpgradeEvent.UpgradeIssued);
            // Now we wait for the next HTTP response to see if we switch protocols.
            return task;
        }

        protected override void Decode(IChannelHandlerContext context, IHttpObject message, List<object> output)
        {
            IFullHttpResponse response = null;
            try
            {
                if (!this.upgradeRequested)
                {
                    throw new InvalidOperationException("Read HTTP response without requesting protocol switch");
                }

                if (message is IHttpResponse rep)
                {
                    if (!HttpResponseStatus.SwitchingProtocols.Equals(rep.Status))
                    {
                        // The server does not support the requested protocol, just remove this handler
                        // and continue processing HTTP.
                        // NOTE: not releasing the response since we're letting it propagate to the
                        // next handler.
                        context.FireUserEventTriggered(UpgradeEvent.UpgradeRejected);
                        RemoveThisHandler(context);
                        context.FireChannelRead(rep);
                        return;
                    }
                }

                if (message is IFullHttpResponse fullRep)
                {
                    response = fullRep;
                    // Need to retain since the base class will release after returning from this method.
                    response.Retain();
                    output.Add(response);
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

                    Debug.Assert(output.Count == 1);
                    response = (IFullHttpResponse)output[0];
                }

                if (response.Headers.TryGet(HttpHeaderNames.Upgrade, out ICharSequence upgradeHeader) && !AsciiString.ContentEqualsIgnoreCase(this.upgradeCodec.Protocol, upgradeHeader))
                {
                    throw new  InvalidOperationException($"Switching Protocols response with unexpected UPGRADE protocol: {upgradeHeader}");
                }

                // Upgrade to the new protocol.
                this.sourceCodec.PrepareUpgradeFrom(context);
                this.upgradeCodec.UpgradeTo(context, response);

                // Notify that the upgrade to the new protocol completed successfully.
                context.FireUserEventTriggered(UpgradeEvent.UpgradeSuccessful);

                // We guarantee UPGRADE_SUCCESSFUL event will be arrived at the next handler
                // before http2 setting frame and http response.
                this.sourceCodec.UpgradeFrom(context);

                // We switched protocols, so we're done with the upgrade response.
                // Release it and clear it from the output.
                response.Release();
                output.Clear();
                RemoveThisHandler(context);
            }
            catch (Exception exception)
            {
                ReferenceCountUtil.Release(response);
                context.FireExceptionCaught(exception);
                RemoveThisHandler(context);
            }
        }

        static void RemoveThisHandler(IChannelHandlerContext ctx) => ctx.Channel.Pipeline.Remove(ctx.Name);

        void SetUpgradeRequestHeaders(IChannelHandlerContext ctx, IHttpRequest request)
        {
            // Set the UPGRADE header on the request.
            request.Headers.Set(HttpHeaderNames.Upgrade, this.upgradeCodec.Protocol);

            // Add all protocol-specific headers to the request.
            var connectionParts = new List<ICharSequence>(2);
            connectionParts.AddRange(this.upgradeCodec.SetUpgradeHeaders(ctx, request));

            // Set the CONNECTION header from the set of all protocol-specific headers that were added.
            var builder = new StringBuilder();
            foreach (ICharSequence part in connectionParts)
            {
                builder.Append(part);
                builder.Append(',');
            }
            builder.Append(HttpHeaderValues.Upgrade);
            request.Headers.Set(HttpHeaderNames.Connection, new StringCharSequence(builder.ToString()));
        }
    }
}
