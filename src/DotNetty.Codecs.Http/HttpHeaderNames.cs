// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using DotNetty.Common.Utilities;

    ///
    /// Standard HTTP header names.
    /// 
    /// These are all defined as lowercase to support HTTP/2 requirements while also not
    /// violating HTTP/1.x requirements.New header names should always be lowercase.
    /// 
    public static class HttpHeaderNames
    {
        public static readonly AsciiString Accept = AsciiString.Cached("accept");

        public static readonly AsciiString AcceptCharset = AsciiString.Cached("accept-charset");

        public static readonly AsciiString AcceptEncoding = AsciiString.Cached("accept-encoding");

        public static readonly AsciiString AcceptLanguage = AsciiString.Cached("accept-language");

        public static readonly AsciiString AcceptRanges = AsciiString.Cached("accept-ranges");

        public static readonly AsciiString AcceptPatch = AsciiString.Cached("accept-patch");
        
        public static readonly AsciiString AccessControlAllowCredentials = AsciiString.Cached("access-control-allow-credentials");

        public static readonly AsciiString AccessControlAllowHeaders = AsciiString.Cached("access-control-allow-headers");

        public static readonly AsciiString AccessControlAllowMethods = AsciiString.Cached("access-control-allow-methods");

        public static readonly AsciiString AccessControlAllowOrigin = AsciiString.Cached("access-control-allow-origin");

        public static readonly AsciiString AccessControlExposeHeaders = AsciiString.Cached("access-control-expose-headers");

        public static readonly AsciiString AccessControlMaxAge = AsciiString.Cached("access-control-max-age");

        public static readonly AsciiString AccessControlRequestHeaders = AsciiString.Cached("access-control-request-headers");

        public static readonly AsciiString AccessControlRequestMethod = AsciiString.Cached("access-control-request-method");

        public static readonly AsciiString Age = AsciiString.Cached("age");

        public static readonly AsciiString Allow = AsciiString.Cached("allow");

        public static readonly AsciiString Authorization = AsciiString.Cached("authorization");

        public static readonly AsciiString CacheControl = AsciiString.Cached("cache-control");

        public static readonly AsciiString Connection = AsciiString.Cached("connection");

        public static readonly AsciiString ContentBase = AsciiString.Cached("content-base");

        public static readonly AsciiString ContentEncoding = AsciiString.Cached("content-encoding");

        public static readonly AsciiString ContentLanguage = AsciiString.Cached("content-language");

        public static readonly AsciiString ContentLength = AsciiString.Cached("content-length");

        public static readonly AsciiString ContentLocation = AsciiString.Cached("content-location");

        public static readonly AsciiString ContentTransferEncoding = AsciiString.Cached("content-transfer-encoding");

        public static readonly AsciiString ContentDisposition = AsciiString.Cached("content-disposition");

        public static readonly AsciiString ContentMD5 = AsciiString.Cached("content-md5");

        public static readonly AsciiString ContentRange = AsciiString.Cached("content-range");

        public static readonly AsciiString ContentSecurityPolicy = AsciiString.Cached("content-security-policy");

        public static readonly AsciiString ContentType = AsciiString.Cached("content-type");

        public static readonly AsciiString Cookie = AsciiString.Cached("cookie");

        public static readonly AsciiString Date = AsciiString.Cached("date");

        public static readonly AsciiString Etag = AsciiString.Cached("etag");

        public static readonly AsciiString Expect = AsciiString.Cached("expect");

        public static readonly AsciiString Expires = AsciiString.Cached("expires");

        public static readonly AsciiString From = AsciiString.Cached("from");

        public static readonly AsciiString Host = AsciiString.Cached("host");

        public static readonly AsciiString IfMatch = AsciiString.Cached("if-match");

        public static readonly AsciiString IfModifiedSince = AsciiString.Cached("if-modified-since");

        public static readonly AsciiString IfNoneMatch = AsciiString.Cached("if-none-match");

        public static readonly AsciiString IfRange = AsciiString.Cached("if-range");

        public static readonly AsciiString IfUnmodifiedSince = AsciiString.Cached("if-unmodified-since");

        public static readonly AsciiString LastModified = AsciiString.Cached("last-modified");

        public static readonly AsciiString Location = AsciiString.Cached("location");

        public static readonly AsciiString MaxForwards = AsciiString.Cached("max-forwards");

        public static readonly AsciiString Origin = AsciiString.Cached("origin");

        public static readonly AsciiString Pragma = AsciiString.Cached("pragma");

        public static readonly AsciiString ProxyAuthenticate = AsciiString.Cached("proxy-authenticate");

        public static readonly AsciiString ProxyAuthorization = AsciiString.Cached("proxy-authorization");

        public static readonly AsciiString Range = AsciiString.Cached("range");

        public static readonly AsciiString Referer = AsciiString.Cached("referer");

        public static readonly AsciiString RetryAfter = AsciiString.Cached("retry-after");

        public static readonly AsciiString SecWebsocketKey1 = AsciiString.Cached("sec-websocket-key1");

        public static readonly AsciiString SecWebsocketKey2 = AsciiString.Cached("sec-websocket-key2");

        public static readonly AsciiString SecWebsocketLocation = AsciiString.Cached("sec-websocket-location");

        public static readonly AsciiString SecWebsocketOrigin = AsciiString.Cached("sec-websocket-origin");

        public static readonly AsciiString SecWebsocketProtocol = AsciiString.Cached("sec-websocket-protocol");

        public static readonly AsciiString SecWebsocketVersion = AsciiString.Cached("sec-websocket-version");

        public static readonly AsciiString SecWebsocketKey = AsciiString.Cached("sec-websocket-key");

        public static readonly AsciiString SecWebsocketAccept = AsciiString.Cached("sec-websocket-accept");

        public static readonly AsciiString SecWebsocketExtensions = AsciiString.Cached("sec-websocket-extensions");

        public static readonly AsciiString Server = AsciiString.Cached("server");

        public static readonly AsciiString SetCookie = AsciiString.Cached("set-cookie");

        public static readonly AsciiString SetCookie2 = AsciiString.Cached("set-cookie2");

        public static readonly AsciiString Te = AsciiString.Cached("te");

        public static readonly AsciiString Trailer = AsciiString.Cached("trailer");

        public static readonly AsciiString TransferEncoding = AsciiString.Cached("transfer-encoding");

        public static readonly AsciiString Upgrade = AsciiString.Cached("upgrade");

        public static readonly AsciiString UserAgent = AsciiString.Cached("user-agent");

        public static readonly AsciiString Vary = AsciiString.Cached("vary");

        public static readonly AsciiString Via = AsciiString.Cached("via");

        public static readonly AsciiString Warning = AsciiString.Cached("warning");

        public static readonly AsciiString WebsocketLocation = AsciiString.Cached("websocket-location");

        public static readonly AsciiString WebsocketOrigin = AsciiString.Cached("websocket-origin");

        public static readonly AsciiString WebsocketProtocol = AsciiString.Cached("websocket-protocol");

        public static readonly AsciiString WwwAuthenticate = AsciiString.Cached("www-authenticate");

        public static readonly AsciiString XFrameOptions = AsciiString.Cached("x-frame-options");
    }
}
