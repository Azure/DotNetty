// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Common.Utilities;

    public static class HttpUtil
    {
        static readonly AsciiString CharsetEquals = new AsciiString(HttpHeaderValues.Charset + "=");
        static readonly AsciiString Semicolon = AsciiString.Cached(";");

        public static bool IsKeepAlive(IHttpMessage message)
        {
            if (message.Headers.TryGet(HttpHeaderNames.Connection, out ICharSequence connection) 
                && HttpHeaderValues.Close.ContentEqualsIgnoreCase(connection))
            {
                return false;
            }

            if (message.ProtocolVersion.IsKeepAliveDefault)
            {
                return !HttpHeaderValues.Close.ContentEqualsIgnoreCase(connection);
            }
            else
            {
                return HttpHeaderValues.KeepAlive.ContentEqualsIgnoreCase(connection);
            }
        }

        public static void SetKeepAlive(IHttpMessage message, bool keepAlive) => SetKeepAlive(message.Headers, message.ProtocolVersion, keepAlive);

        public static void SetKeepAlive(HttpHeaders headers, HttpVersion httpVersion, bool keepAlive)
        {
            if (httpVersion.IsKeepAliveDefault)
            {
                if (keepAlive)
                {
                    headers.Remove(HttpHeaderNames.Connection);
                }
                else
                {
                    headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
                }
            }
            else
            {
                if (keepAlive)
                {
                    headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
                }
                else
                {
                    headers.Remove(HttpHeaderNames.Connection);
                }
            }
        }

        public static long GetContentLength(IHttpMessage message)
        {
            if (message.Headers.TryGet(HttpHeaderNames.ContentLength, out ICharSequence value))
            {
                return CharUtil.ParseLong(value);
            }

            // We know the content length if it's a Web Socket message even if
            // Content-Length header is missing.
            long webSocketContentLength = GetWebSocketContentLength(message);
            if (webSocketContentLength >= 0)
            {
                return webSocketContentLength;
            }

            // Otherwise we don't.
            throw new FormatException($"header not found: {HttpHeaderNames.ContentLength}");
        }

        public static long GetContentLength(IHttpMessage message, long defaultValue)
        {
            if (message.Headers.TryGet(HttpHeaderNames.ContentLength, out ICharSequence value))
            {
                return CharUtil.ParseLong(value);
            }

            // We know the content length if it's a Web Socket message even if
            // Content-Length header is missing.
            long webSocketContentLength = GetWebSocketContentLength(message);
            if (webSocketContentLength >= 0)
            {
                return webSocketContentLength;
            }

            // Otherwise we don't.
            return defaultValue;
        }

        public static int GetContentLength(IHttpMessage message, int defaultValue) => 
            (int)Math.Min(int.MaxValue, GetContentLength(message, (long)defaultValue));

        static int GetWebSocketContentLength(IHttpMessage message)
        {
            // WebSocket messages have constant content-lengths.
            HttpHeaders h = message.Headers;
            if (message is IHttpRequest req)
            {
                if (HttpMethod.Get.Equals(req.Method)
                    && h.Contains(HttpHeaderNames.SecWebsocketKey1)
                    && h.Contains(HttpHeaderNames.SecWebsocketKey2))
                {
                    return 8;
                }
            }
            else if (message is IHttpResponse res)
            {
                if (res.Status.Code == 101
                    && h.Contains(HttpHeaderNames.SecWebsocketOrigin)
                    && h.Contains(HttpHeaderNames.SecWebsocketLocation))
                {
                    return 16;
                }
            }

            // Not a web socket message
            return -1;
        }

        public static void SetContentLength(IHttpMessage message, long length) => message.Headers.Set(HttpHeaderNames.ContentLength, length);

        public static bool IsContentLengthSet(IHttpMessage message) => message.Headers.Contains(HttpHeaderNames.ContentLength);

        public static bool Is100ContinueExpected(IHttpMessage message)
        {
            if (!IsExpectHeaderValid(message))
            {
                return false;
            }

            ICharSequence expectValue = message.Headers.Get(HttpHeaderNames.Expect, null);
            // unquoted tokens in the expect header are case-insensitive, thus 100-continue is case insensitive
            return HttpHeaderValues.Continue.ContentEqualsIgnoreCase(expectValue);
        }

        internal static bool IsUnsupportedExpectation(IHttpMessage message)
        {
            if (!IsExpectHeaderValid(message))
            {
                return false;
            }

            return message.Headers.TryGet(HttpHeaderNames.Expect, out ICharSequence expectValue) 
                && !HttpHeaderValues.Continue.ContentEqualsIgnoreCase(expectValue);
        }

        // Expect: 100-continue is for requests only and it works only on HTTP/1.1 or later. Note further that RFC 7231
        // section 5.1.1 says "A server that receives a 100-continue expectation in an HTTP/1.0 request MUST ignore
        // that expectation."
        static bool IsExpectHeaderValid(IHttpMessage message) => message is IHttpRequest
            && message.ProtocolVersion.CompareTo(HttpVersion.Http11) >= 0;

        public static void Set100ContinueExpected(IHttpMessage message, bool expected)
        {
            if (expected)
            {
                message.Headers.Set(HttpHeaderNames.Expect, HttpHeaderValues.Continue);
            }
            else
            {
                message.Headers.Remove(HttpHeaderNames.Expect);
            }
        }

        public static bool IsTransferEncodingChunked(IHttpMessage message) => message.Headers.Contains(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked, true);

        public static void SetTransferEncodingChunked(IHttpMessage m, bool chunked)
        {
            if (chunked)
            {
                m.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
                m.Headers.Remove(HttpHeaderNames.ContentLength);
            }
            else
            {
                IList<ICharSequence> encodings = m.Headers.GetAll(HttpHeaderNames.TransferEncoding);
                if (encodings.Count == 0)
                {
                    return;
                }
                var values = new List<ICharSequence>(encodings);
                foreach (ICharSequence value in encodings)
                {
                    if (HttpHeaderValues.Chunked.ContentEqualsIgnoreCase(value))
                    {
                        values.Remove(value);
                    }
                }
                if (values.Count == 0)
                {
                    m.Headers.Remove(HttpHeaderNames.TransferEncoding);
                }
                else
                {
                    m.Headers.Set(HttpHeaderNames.TransferEncoding, values);
                }
            }
        }

        public static Encoding GetCharset(IHttpMessage message) => GetCharset(message, Encoding.UTF8);

        public static Encoding GetCharset(ICharSequence contentTypeValue) => contentTypeValue != null ? GetCharset(contentTypeValue, Encoding.UTF8) : Encoding.UTF8;

        public static Encoding GetCharset(IHttpMessage message, Encoding defaultCharset)
        {
            return message.Headers.TryGet(HttpHeaderNames.ContentType, out ICharSequence contentTypeValue) 
                ? GetCharset(contentTypeValue, defaultCharset) 
                : defaultCharset;
        }

        public static Encoding GetCharset(ICharSequence contentTypeValue, Encoding defaultCharset)
        {
            if (contentTypeValue != null)
            {
                ICharSequence charsetCharSequence = GetCharsetAsSequence(contentTypeValue);
                if (charsetCharSequence != null)
                {
                    try
                    {
                        return Encoding.GetEncoding(charsetCharSequence.ToString());
                    }
                    catch (ArgumentException)
                    {
                        return defaultCharset;
                    }
                }
                else
                {
                    return defaultCharset;
                }
            }
            else
            {
                return defaultCharset;
            }
        }

        public static ICharSequence GetCharsetAsSequence(IHttpMessage message) 
            => message.Headers.TryGet(HttpHeaderNames.ContentType, out ICharSequence contentTypeValue) ? GetCharsetAsSequence(contentTypeValue) : null;

        public static ICharSequence GetCharsetAsSequence(ICharSequence contentTypeValue)
        {
            if (contentTypeValue == null)
            {
                throw new ArgumentException(nameof(contentTypeValue));
            }
            int indexOfCharset = AsciiString.IndexOfIgnoreCaseAscii(contentTypeValue, CharsetEquals, 0);
            if (indexOfCharset != AsciiString.IndexNotFound)
            {
                int indexOfEncoding = indexOfCharset + CharsetEquals.Count;
                if (indexOfEncoding < contentTypeValue.Count)
                {
                    return contentTypeValue.SubSequence(indexOfEncoding, contentTypeValue.Count);
                }
            }
            return null;
        }

        public static ICharSequence GetMimeType(IHttpMessage message) => 
            message.Headers.TryGet(HttpHeaderNames.ContentType, out ICharSequence contentTypeValue) ? GetMimeType(contentTypeValue) : null;

        public static ICharSequence GetMimeType(ICharSequence contentTypeValue)
        {
            if (contentTypeValue == null)
            {
                throw new ArgumentException(nameof(contentTypeValue));
            }
            int indexOfSemicolon = AsciiString.IndexOfIgnoreCaseAscii(contentTypeValue, Semicolon, 0);
            if (indexOfSemicolon != AsciiString.IndexNotFound)
            {
                return contentTypeValue.SubSequence(0, indexOfSemicolon);
            }

            return contentTypeValue.Count > 0 ? contentTypeValue : null;
        }
    }
}
