// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;

    using static Http.HttpVersion;

    public class WebSocketRequestBuilder
    {
        HttpVersion httpVersion;
        HttpMethod method;
        string uri;
        string host;
        string upgrade;
        string connection;
        string key;
        string origin;
        WebSocketVersion version;

        public WebSocketRequestBuilder HttpVersion(HttpVersion value)
        {
            this.httpVersion = value;
            return this;
        }

        public WebSocketRequestBuilder Method(HttpMethod value)
        {
            this.method = value;
            return this;
        }

        public WebSocketRequestBuilder Uri(string value)
        {
            this.uri = value;
            return this;
        }

        public WebSocketRequestBuilder Host(string value)
        {
            this.host = value;
            return this;
        }

        public WebSocketRequestBuilder Upgrade(string value)
        {
            this.upgrade = value;
            return this;
        }

        public WebSocketRequestBuilder Upgrade(AsciiString value)
        {
            this.upgrade = this.upgrade == null ? null : value.ToString();
            return this;
        }

        public WebSocketRequestBuilder Connection(string value)
        {
            this.connection = value;
            return this;
        }

        public WebSocketRequestBuilder Key(string value)
        {
            this.key = value;
            return this;
        }

        public WebSocketRequestBuilder Origin(string value)
        {
            this.origin = value;
            return this;
        }

        public WebSocketRequestBuilder Version13()
        {
            this.version = WebSocketVersion.V13;
            return this;
        }

        public WebSocketRequestBuilder Version8()
        {
            this.version = WebSocketVersion.V08;
            return this;
        }

        public WebSocketRequestBuilder Version00()
        {
            this.version = null;
            return this;
        }

        public WebSocketRequestBuilder NoVersion()
        {
            return this;
        }

        public IFullHttpRequest Build()
        {
            var req = new DefaultFullHttpRequest(this.httpVersion, this.method, this.uri);
            HttpHeaders headers = req.Headers;

            if (this.host != null)
            {
                headers.Set(HttpHeaderNames.Host, this.host);
            }
            if (this.upgrade != null)
            {
                headers.Set(HttpHeaderNames.Upgrade, this.upgrade);
            }
            if (this.connection != null)
            {
                headers.Set(HttpHeaderNames.Connection, this.connection);
            }
            if (this.key != null)
            {
                headers.Set(HttpHeaderNames.SecWebsocketKey, this.key);
            }
            if (this.origin != null)
            {
                headers.Set(HttpHeaderNames.SecWebsocketOrigin, this.origin);
            }
            if (this.version != null)
            {
                headers.Set(HttpHeaderNames.SecWebsocketVersion, this.version.ToHttpHeaderValue());
            }
            return req;
        }

        public static IHttpRequest Successful() => new WebSocketRequestBuilder()
            .HttpVersion(Http11)
            .Method(HttpMethod.Get)
            .Uri("/test")
            .Host("server.example.com")
            .Upgrade(HttpHeaderValues.Websocket)
            .Key("dGhlIHNhbXBsZSBub25jZQ==")
            .Origin("http://example.com")
            .Version13()
            .Build();
    }
}
