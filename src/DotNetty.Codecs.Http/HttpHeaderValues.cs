// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using DotNetty.Common.Utilities;

    public static class HttpHeaderValues
    {
        public static readonly AsciiString ApplicationJson = AsciiString.Cached("application/json");

        public static readonly AsciiString ApplicationXWwwFormUrlencoded = AsciiString.Cached("application/x-www-form-urlencoded");

        public static readonly AsciiString ApplicationOctetStream = AsciiString.Cached("application/octet-stream");

        public static readonly AsciiString Attachment = AsciiString.Cached("attachment");

        public static readonly AsciiString Base64 = AsciiString.Cached("base64");

        public static readonly AsciiString Binary = AsciiString.Cached("binary");

        public static readonly AsciiString Boundary = AsciiString.Cached("boundary");

        public static readonly AsciiString Bytes = AsciiString.Cached("bytes");

        public static readonly AsciiString Charset = AsciiString.Cached("charset");

        public static readonly AsciiString Chunked = AsciiString.Cached("chunked");

        public static readonly AsciiString Close = AsciiString.Cached("close");

        public static readonly AsciiString Compress = AsciiString.Cached("compress");

        public static readonly AsciiString Continue = AsciiString.Cached("100-continue");

        public static readonly AsciiString Deflate = AsciiString.Cached("deflate");

        public static readonly AsciiString XDeflate = AsciiString.Cached("x-deflate");

        public static readonly AsciiString File = AsciiString.Cached("file");

        public static readonly AsciiString FileName = AsciiString.Cached("filename");

        public static readonly AsciiString FormData = AsciiString.Cached("form-data");

        public static readonly AsciiString Gzip = AsciiString.Cached("gzip");

        public static readonly AsciiString GzipDeflate = AsciiString.Cached("gzip,deflate");

        public static readonly AsciiString XGzip = AsciiString.Cached("x-gzip");

        public static readonly AsciiString Identity = AsciiString.Cached("identity");

        public static readonly AsciiString KeepAlive = AsciiString.Cached("keep-alive");

        public static readonly AsciiString MaxAge = AsciiString.Cached("max-age");

        public static readonly AsciiString MaxStale = AsciiString.Cached("max-stale");

        public static readonly AsciiString MinFresh = AsciiString.Cached("min-fresh");

        public static readonly AsciiString MultipartFormData = AsciiString.Cached("multipart/form-data");

        public static readonly AsciiString MultipartMixed = AsciiString.Cached("multipart/mixed");

        public static readonly AsciiString MustRevalidate = AsciiString.Cached("must-revalidate");

        public static readonly AsciiString Name = AsciiString.Cached("name");

        public static readonly AsciiString NoCache = AsciiString.Cached("no-cache");

        public static readonly AsciiString NoStore = AsciiString.Cached("no-store");

        public static readonly AsciiString NoTransform = AsciiString.Cached("no-transform");

        public static readonly AsciiString None = AsciiString.Cached("none");

        public static readonly AsciiString Zero = AsciiString.Cached("0");

        public static readonly AsciiString OnlyIfCached = AsciiString.Cached("only-if-cached");

        public static readonly AsciiString Private = AsciiString.Cached("private");

        public static readonly AsciiString ProxyRevalidate = AsciiString.Cached("proxy-revalidate");

        public static readonly AsciiString Public = AsciiString.Cached("public");

        public static readonly AsciiString QuotedPrintable = AsciiString.Cached("quoted-printable");

        public static readonly AsciiString SMaxage = AsciiString.Cached("s-maxage");

        public static readonly AsciiString TextPlain = AsciiString.Cached("text/plain");

        public static readonly AsciiString Trailers = AsciiString.Cached("trailers");

        public static readonly AsciiString Upgrade = AsciiString.Cached("upgrade");

        public static readonly AsciiString Websocket = AsciiString.Cached("websocket");
    }
}
