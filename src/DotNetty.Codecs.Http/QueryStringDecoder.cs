// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Common.Utilities;

    public class QueryStringDecoder
    {
        const int DefaultMaxParams = 1024;

        readonly Encoding charset;
        readonly string uri;
        readonly int maxParams;
        int pathEndIdx;
        string path;
        IDictionary<string, List<string>> parameters;

        public QueryStringDecoder(string uri) : this(uri, HttpConstants.DefaultEncoding)
        {
        }

        public QueryStringDecoder(string uri, bool hasPath) : this(uri, HttpConstants.DefaultEncoding, hasPath)
        {
        }

        public QueryStringDecoder(string uri, Encoding charset) : this(uri, charset, true)
        {
        }

        public QueryStringDecoder(string uri, Encoding charset, bool hasPath) : this(uri, charset, hasPath, DefaultMaxParams)
        {
        }

        public QueryStringDecoder(string uri, Encoding charset, bool hasPath, int maxParams)
        {
            Contract.Requires(uri != null);
            Contract.Requires(charset != null);
            Contract.Requires(maxParams > 0);

            this.uri = uri;
            this.charset = charset;
            this.maxParams = maxParams;

            // -1 means that path end index will be initialized lazily
            this.pathEndIdx = hasPath ? -1 : 0;
        }

        public QueryStringDecoder(Uri uri) : this(uri, HttpConstants.DefaultEncoding)
        {
        }

        public QueryStringDecoder(Uri uri, Encoding charset) : this(uri, charset, DefaultMaxParams)
        {
        }

        public QueryStringDecoder(Uri uri, Encoding charset, int maxParams)
        {
            Contract.Requires(uri != null);
            Contract.Requires(charset != null);
            Contract.Requires(maxParams > 0);

            string rawPath = uri.AbsolutePath;
            // Also take care of cut of things like "http://localhost"
            this.uri = uri.PathAndQuery;
            this.charset = charset;
            this.maxParams = maxParams;
            this.pathEndIdx = rawPath.Length;
        }

        public override string ToString() => this.uri;

        public string Path => this.path ?? 
            (this.path = DecodeComponent(this.uri, 0, this.PathEndIdx(), this.charset, true));

        public IDictionary<string, List<string>> Parameters => this.parameters ?? 
            (this.parameters = DecodeParams(this.uri, this.PathEndIdx(), this.charset, this.maxParams));

        public string RawPath() => this.uri.Substring(0, this.PathEndIdx());

        public string RawQuery() 
        {
            int start = this.pathEndIdx + 1;
            return start < this.uri.Length ? this.uri.Substring(start) : StringUtil.EmptyString;
        }

        int PathEndIdx()
        {
            if (this.pathEndIdx == -1)
            {
                this.pathEndIdx = FindPathEndIndex(this.uri);
            }
            return this.pathEndIdx;
        }

        static IDictionary<string, List<string>> DecodeParams(string s, int from, Encoding charset, int paramsLimit)
        {
            int len = s.Length;
            if (from >= len)
            {
                return ImmutableDictionary<string, List<string>>.Empty;
            }
            if (s[from] == '?')
            {
                from++;
            }
            var parameters = new Dictionary<string, List<string>>();
            int nameStart = from;
            int valueStart = -1;
            int i;
            //loop:
            for (i = from; i < len; i++)
            {
                switch (s[i])
                {
                    case '=':
                        if (nameStart == i)
                        {
                            nameStart = i + 1;
                        }
                        else if (valueStart < nameStart)
                        {
                            valueStart = i + 1;
                        }
                        break;
                    case '&':
                    case ';':
                        if (AddParam(s, nameStart, valueStart, i, parameters, charset))
                    {
                        paramsLimit--;
                        if (paramsLimit == 0)
                        {
                            return parameters;
                        }
                    }
                        nameStart = i + 1;
                        break;
                    case '#':
                        goto loop;
                }
            }
            loop:
            AddParam(s, nameStart, valueStart, i, parameters, charset);
            return parameters;
        }

        static bool AddParam(string s, int nameStart, int valueStart, int valueEnd,
            Dictionary<string, List<string>> parameters, Encoding charset)
        {
            if (nameStart >= valueEnd)
            {
                return false;
            }
            if (valueStart <= nameStart)
            {
                valueStart = valueEnd + 1;
            }
            string name = DecodeComponent(s, nameStart, valueStart - 1, charset, false);
            string value = DecodeComponent(s, valueStart, valueEnd, charset, false);
            if (!parameters.TryGetValue(name, out List<string> values))
            {
                values = new List<string>(1);  // Often there's only 1 value.
                parameters.Add(name, values);
            }
            values.Add(value);
            return true;
        }

        public static string DecodeComponent(string s) => DecodeComponent(s, HttpConstants.DefaultEncoding);

        public static string DecodeComponent(string s, Encoding charset) => s == null 
            ? StringUtil.EmptyString : DecodeComponent(s, 0, s.Length, charset, false);

        static string DecodeComponent(string s, int from, int toExcluded, Encoding charset, bool isPath)
        {
            int len = toExcluded - from;
            if (len <= 0)
            {
                return StringUtil.EmptyString;
            }
            int firstEscaped = -1;
            for (int i = from; i < toExcluded; i++)
            {
                char c = s[i];
                if (c == '%' || c == '+' && !isPath)
                {
                    firstEscaped = i;
                    break;
                }
            }
            if (firstEscaped == -1)
            {
                return s.Substring(from, len);
            }

            // Each encoded byte takes 3 characters (e.g. "%20")
            int decodedCapacity = (toExcluded - firstEscaped) / 3;
            var byteBuf = new byte[decodedCapacity];
            int idx;
            var strBuf = new StringBuilder(len);
            strBuf.Append(s, from, firstEscaped - from);

            for (int i = firstEscaped; i < toExcluded; i++)
            {
                char c = s[i];
                if (c != '%')
                {
                    strBuf.Append(c != '+' || isPath ? c : StringUtil.Space);
                    continue;
                }

                idx = 0;
                do
                {
                    if (i + 3 > toExcluded)
                    {
                        throw new ArgumentException($"unterminated escape sequence at index {i} of: {s}");
                    }
                    byteBuf[idx++] = StringUtil.DecodeHexByte(s, i + 1);
                    i += 3;
                }
                while (i < toExcluded && s[i] == '%');
                i--;

                strBuf.Append(charset.GetString(byteBuf, 0, idx));
            }

            return strBuf.ToString();
        }

        static int FindPathEndIndex(string uri)
        {
            int len = uri.Length;
            for (int i = 0; i < len; i++)
            {
                char c = uri[i];
                if (c == '?' || c == '#')
                {
                    return i;
                }
            }
            return len;
        }
    }
}
