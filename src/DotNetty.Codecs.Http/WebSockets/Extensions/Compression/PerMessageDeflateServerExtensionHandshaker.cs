// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs.Compression;

    public sealed class PerMessageDeflateServerExtensionHandshaker : IWebSocketServerExtensionHandshaker
    {
        public static readonly int MinWindowSize = 8;
        public static readonly int MaxWindowSize = 15;

        internal static readonly string PerMessageDeflateExtension = "permessage-deflate";
        internal static readonly string ClientMaxWindow = "client_max_window_bits";
        internal static readonly string ServerMaxWindow = "server_max_window_bits";
        internal static readonly string ClientNoContext = "client_no_context_takeover";
        internal static readonly string ServerNoContext = "server_no_context_takeover";

        readonly int compressionLevel;
        readonly bool allowServerWindowSize;
        readonly int preferredClientWindowSize;
        readonly bool allowServerNoContext;
        readonly bool preferredClientNoContext;

        public PerMessageDeflateServerExtensionHandshaker()
            : this(6, ZlibCodecFactory.IsSupportingWindowSizeAndMemLevel, MaxWindowSize, false, false)
        {
        }

        public PerMessageDeflateServerExtensionHandshaker(int compressionLevel,
            bool allowServerWindowSize, int preferredClientWindowSize,
            bool allowServerNoContext, bool preferredClientNoContext)
        {
            if (preferredClientWindowSize > MaxWindowSize || preferredClientWindowSize < MinWindowSize)
            {
                throw new ArgumentException($"preferredServerWindowSize: {preferredClientWindowSize} (expected: 8-15)");
            }
            if (compressionLevel < 0 || compressionLevel > 9)
            {
                throw new ArgumentException($"compressionLevel: {compressionLevel} (expected: 0-9)");
            }
            this.compressionLevel = compressionLevel;
            this.allowServerWindowSize = allowServerWindowSize;
            this.preferredClientWindowSize = preferredClientWindowSize;
            this.allowServerNoContext = allowServerNoContext;
            this.preferredClientNoContext = preferredClientNoContext;
        }

        public IWebSocketServerExtension HandshakeExtension(WebSocketExtensionData extensionData)
        {
            if (!PerMessageDeflateExtension.Equals(extensionData.Name))
            {
                return null;
            }

            bool deflateEnabled = true;
            int clientWindowSize = MaxWindowSize;
            int serverWindowSize = MaxWindowSize;
            bool serverNoContext = false;
            bool clientNoContext = false;

            foreach (KeyValuePair<string, string> parameter in extensionData.Parameters)
            {
                if (ClientMaxWindow.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // use preferred clientWindowSize because client is compatible with customization
                    clientWindowSize = this.preferredClientWindowSize;
                }
                else if (ServerMaxWindow.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // use provided windowSize if it is allowed
                    if (this.allowServerWindowSize)
                    {
                        serverWindowSize = int.Parse(parameter.Value);
                        if (serverWindowSize > MaxWindowSize || serverWindowSize < MinWindowSize)
                        {
                            deflateEnabled = false;
                        }
                    }
                    else
                    {
                        deflateEnabled = false;
                    }
                }
                else if (ClientNoContext.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // use preferred clientNoContext because client is compatible with customization
                    clientNoContext = this.preferredClientNoContext;
                }
                else if (ServerNoContext.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // use server no context if allowed
                    if (this.allowServerNoContext)
                    {
                        serverNoContext = true;
                    }
                    else
                    {
                        deflateEnabled = false;
                    }
                }
                else
                {
                    // unknown parameter
                    deflateEnabled = false;
                }
                if (!deflateEnabled)
                {
                    break;
                }
            }

            if (deflateEnabled)
            {
                return new WebSocketPermessageDeflateExtension(this.compressionLevel, serverNoContext,
                    serverWindowSize, clientNoContext, clientWindowSize);
            }
            else
            {
                return null;
            }
        }

        sealed class WebSocketPermessageDeflateExtension : IWebSocketServerExtension
        {
            readonly int compressionLevel;
            readonly bool serverNoContext;
            readonly int serverWindowSize;
            readonly bool clientNoContext;
            readonly int clientWindowSize;

            public WebSocketPermessageDeflateExtension(int compressionLevel, bool serverNoContext,
                int serverWindowSize, bool clientNoContext, int clientWindowSize)
            {
                this.compressionLevel = compressionLevel;
                this.serverNoContext = serverNoContext;
                this.serverWindowSize = serverWindowSize;
                this.clientNoContext = clientNoContext;
                this.clientWindowSize = clientWindowSize;
            }

            public int Rsv => WebSocketRsv.Rsv1;

            public WebSocketExtensionEncoder NewExtensionEncoder() => 
                new PerMessageDeflateEncoder(this.compressionLevel, this.clientWindowSize, this.clientNoContext);

            public WebSocketExtensionDecoder NewExtensionDecoder() => new PerMessageDeflateDecoder(this.serverNoContext);

            public WebSocketExtensionData NewReponseData()
            {
                var parameters = new Dictionary<string, string>(4);
                if (this.serverNoContext)
                {
                    parameters.Add(ServerNoContext, null);
                }
                if (this.clientNoContext)
                {
                    parameters.Add(ClientNoContext, null);
                }
                if (this.serverWindowSize != MaxWindowSize)
                {
                    parameters.Add(ServerMaxWindow, Convert.ToString(this.serverWindowSize));
                }
                if (this.clientWindowSize != MaxWindowSize)
                {
                    parameters.Add(ClientMaxWindow, Convert.ToString(this.clientWindowSize));
                }
                return new WebSocketExtensionData(PerMessageDeflateExtension, parameters);
            }
        }
    }
}
