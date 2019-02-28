// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    public sealed class WebSocketVersion
    {
        public static readonly WebSocketVersion Unknown = new WebSocketVersion("");

        // http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-00
        // draft-ietf-hybi-thewebsocketprotocol- 00.
        public static readonly WebSocketVersion V00 = new WebSocketVersion("0");

        // http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-07
        // draft-ietf-hybi-thewebsocketprotocol- 07
        public static readonly WebSocketVersion V07 = new WebSocketVersion("7");

        // http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-10
        // draft-ietf-hybi-thewebsocketprotocol- 10
        public static readonly WebSocketVersion V08 = new WebSocketVersion("8");

        // http://tools.ietf.org/html/rfc6455 This was originally
        // http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-17 
        //draft-ietf-hybi-thewebsocketprotocol- 17>
        public static readonly WebSocketVersion V13 = new WebSocketVersion("13");

        readonly AsciiString value;

        WebSocketVersion(string value)
        {
            this.value = AsciiString.Cached(value);
        }

        public override string ToString() => this.value.ToString();

        public AsciiString ToHttpHeaderValue()
        {
            ThrowIfUnknown(this);
            return this.value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIfUnknown(WebSocketVersion webSocketVersion)
        {
            if (webSocketVersion == Unknown)
            {
                throw new InvalidOperationException("Unknown web socket version");
            }
        }
    }
}
