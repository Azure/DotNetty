// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable InconsistentNaming
namespace DotNetty.Codecs.Http.WebSockets
{
    using DotNetty.Common.Utilities;

    public sealed class WebSocketScheme
    {
        // Scheme for non-secure WebSocket connection.
        public static readonly WebSocketScheme WS = new WebSocketScheme(80, "ws");

        // Scheme for secure WebSocket connection.
        public static readonly WebSocketScheme WSS = new WebSocketScheme(443, "wss");

        readonly int port;
        readonly AsciiString name;

        WebSocketScheme(int port, string name)
        {
            this.port = port;
            this.name = AsciiString.Cached(name);
        }

        public AsciiString Name => this.name;

        public int Port => this.port;

        public override bool Equals(object obj) => obj is WebSocketScheme other 
            && other.port == this.port && other.name.Equals(this.name);

        public override int GetHashCode() => this.port * 31 + this.name.GetHashCode();

        public override string ToString() => this.name.ToString();
    }
}
