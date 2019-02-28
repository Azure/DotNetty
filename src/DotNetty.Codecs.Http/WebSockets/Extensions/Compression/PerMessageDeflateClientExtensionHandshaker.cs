// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.Compression;

    using static PerMessageDeflateServerExtensionHandshaker;

    public sealed class PerMessageDeflateClientExtensionHandshaker : IWebSocketClientExtensionHandshaker
    {
        readonly int compressionLevel;
        readonly bool allowClientWindowSize;
        readonly int requestedServerWindowSize;
        readonly bool allowClientNoContext;
        readonly bool requestedServerNoContext;

        public PerMessageDeflateClientExtensionHandshaker()
            : this(6, ZlibCodecFactory.IsSupportingWindowSizeAndMemLevel, MaxWindowSize, false, false)
        {
        }

        public PerMessageDeflateClientExtensionHandshaker(int compressionLevel,
            bool allowClientWindowSize, int requestedServerWindowSize,
            bool allowClientNoContext, bool requestedServerNoContext)
        {
            if (requestedServerWindowSize > MaxWindowSize || requestedServerWindowSize < MinWindowSize)
            {
                throw new ArgumentException($"requestedServerWindowSize: {requestedServerWindowSize} (expected: 8-15)");
            }
            if (compressionLevel < 0 || compressionLevel > 9)
            {
                throw new ArgumentException($"compressionLevel: {compressionLevel} (expected: 0-9)");
            }
            this.compressionLevel = compressionLevel;
            this.allowClientWindowSize = allowClientWindowSize;
            this.requestedServerWindowSize = requestedServerWindowSize;
            this.allowClientNoContext = allowClientNoContext;
            this.requestedServerNoContext = requestedServerNoContext;
        }

        public WebSocketExtensionData NewRequestData()
        {
            var parameters = new Dictionary<string, string>(4);
            if (this.requestedServerWindowSize != MaxWindowSize)
            {
                parameters.Add(ServerNoContext, null);
            }
            if (this.allowClientNoContext)
            {
                parameters.Add(ClientNoContext, null);
            }
            if (this.requestedServerWindowSize != MaxWindowSize)
            {
                parameters.Add(ServerMaxWindow, Convert.ToString(this.requestedServerWindowSize));
            }
            if (this.allowClientWindowSize)
            {
                parameters.Add(ClientMaxWindow, null);
            }
            return new WebSocketExtensionData(PerMessageDeflateExtension, parameters);
        }

        public IWebSocketClientExtension HandshakeExtension(WebSocketExtensionData extensionData)
        {
            if (!PerMessageDeflateExtension.Equals(extensionData.Name))
            {
                return null;
            }

            bool succeed = true;
            int clientWindowSize = MaxWindowSize;
            int serverWindowSize = MaxWindowSize;
            bool serverNoContext = false;
            bool clientNoContext = false;

            foreach (KeyValuePair<string, string> parameter in extensionData.Parameters)
            {
                if (ClientMaxWindow.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // allowed client_window_size_bits
                    if (this.allowClientWindowSize)
                    {
                        clientWindowSize = int.Parse(parameter.Value);
                    }
                    else
                    {
                        succeed = false;
                    }
                }
                else if (ServerMaxWindow.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // acknowledged server_window_size_bits
                    serverWindowSize = int.Parse(parameter.Value);
                    if (clientWindowSize > MaxWindowSize || clientWindowSize < MinWindowSize)
                    {
                        succeed = false;
                    }
                }
                else if (ClientNoContext.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // allowed client_no_context_takeover
                    if (this.allowClientNoContext)
                    {
                        clientNoContext = true;
                    }
                    else
                    {
                        succeed = false;
                    }
                }
                else if (ServerNoContext.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // acknowledged server_no_context_takeover
                    if (this.requestedServerNoContext)
                    {
                        serverNoContext = true;
                    }
                    else
                    {
                        succeed = false;
                    }
                }
                else
                {
                    // unknown parameter
                    succeed = false;
                }

                if (!succeed)
                {
                    break;
                }
            }

            if ((this.requestedServerNoContext && !serverNoContext) 
                || this.requestedServerWindowSize != serverWindowSize)
            {
                succeed = false;
            }

            if (succeed)
            {
                return new WebSocketPermessageDeflateExtension(serverNoContext, serverWindowSize,
                    clientNoContext, this.compressionLevel);
            }
            else
            {
                return null;
            }
        }

        sealed class WebSocketPermessageDeflateExtension : IWebSocketClientExtension
        {
            readonly bool serverNoContext;
            readonly int serverWindowSize;
            readonly bool clientNoContext;
            readonly int compressionLevel;

            public int Rsv => WebSocketRsv.Rsv1;

            public WebSocketPermessageDeflateExtension(bool serverNoContext, int serverWindowSize,
                bool clientNoContext, int compressionLevel)
            {
                this.serverNoContext = serverNoContext;
                this.serverWindowSize = serverWindowSize;
                this.clientNoContext = clientNoContext;
                this.compressionLevel = compressionLevel;
            }

            public WebSocketExtensionEncoder NewExtensionEncoder() =>
                new PerMessageDeflateEncoder(this.compressionLevel, this.serverWindowSize, this.serverNoContext);

            public WebSocketExtensionDecoder NewExtensionDecoder() => new PerMessageDeflateDecoder(this.clientNoContext);
        }
    }
}
