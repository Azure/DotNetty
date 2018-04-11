// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Cookies
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Utilities;

    // http://tools.ietf.org/html/rfc6265 
    // compliant cookie decoder to be used server side.
    //
    // http://tools.ietf.org/html/rfc2965 
    // cookies are still supported,old fields will simply be ignored.
    public sealed class ServerCookieDecoder : CookieDecoder
    {
        static readonly AsciiString RFC2965Version = new AsciiString("$Version");
        static readonly AsciiString RFC2965Path = new AsciiString($"${CookieHeaderNames.Path}");
        static readonly AsciiString RFC2965Domain = new AsciiString($"${CookieHeaderNames.Domain}");
        static readonly AsciiString RFC2965Port = new AsciiString("$Port");

        //
        // Strict encoder that validates that name and value chars are in the valid scope
        // defined in RFC6265
        //
        public static readonly ServerCookieDecoder StrictDecoder = new ServerCookieDecoder(true);

        //
        // Lax instance that doesn't validate name and value
        //
        public static readonly ServerCookieDecoder LaxDecoder = new ServerCookieDecoder(false);

        ServerCookieDecoder(bool strict) : base(strict)
        {
        }

        public ISet<ICookie> Decode(string header)
        {
            Contract.Requires(header != null);

            int headerLen = header.Length;
            if (headerLen == 0)
            {
                return ImmutableHashSet<ICookie>.Empty;
            }

            var cookies = new SortedSet<ICookie>();

            int i = 0;

            bool rfc2965Style = false;
            if (CharUtil.RegionMatchesIgnoreCase(header, 0, RFC2965Version, 0, RFC2965Version.Count))
            {
                // RFC 2965 style cookie, move to after version value
                i = header.IndexOf(';') + 1;
                rfc2965Style = true;
            }

            // loop
            for (;;)
            {
                // Skip spaces and separators.
                for (;;)
                {
                    if (i == headerLen)
                    {
                        goto loop;
                    }
                    char c = header[i];
                    if (c == '\t' || c == '\n' || c == 0x0b || c == '\f'
                        || c == '\r' || c == ' ' || c == ',' || c == ';')
                    {
                        i++;
                        continue;
                    }
                    break;
                }

                int nameBegin = i;
                int nameEnd;
                int valueBegin;
                int valueEnd;

                for (;;)
                {
                    char curChar = header[i];
                    if (curChar == ';')
                    {
                        // NAME; (no value till ';')
                        nameEnd = i;
                        valueBegin = valueEnd = -1;
                        break;
                    }
                    else if (curChar == '=')
                    {
                        // NAME=VALUE
                        nameEnd = i;
                        i++;
                        if (i == headerLen)
                        {
                            // NAME= (empty value, i.e. nothing after '=')
                            valueBegin = valueEnd = 0;
                            break;
                        }

                        valueBegin = i;
                        // NAME=VALUE;
                        int semiPos = header.IndexOf(';', i);
                        valueEnd = i = semiPos > 0 ? semiPos : headerLen;
                        break;
                    }
                    else
                    {
                        i++;
                    }

                    if (i == headerLen)
                    {
                        // NAME (no value till the end of string)
                        nameEnd = headerLen;
                        valueBegin = valueEnd = -1;
                        break;
                    }
                }

                if (rfc2965Style && (CharUtil.RegionMatches(header, nameBegin, RFC2965Path, 0, RFC2965Path.Count) 
                        || CharUtil.RegionMatches(header, nameBegin, RFC2965Domain, 0, RFC2965Domain.Count) 
                        || CharUtil.RegionMatches(header, nameBegin, RFC2965Port, 0, RFC2965Port.Count)))
                {
                    // skip obsolete RFC2965 fields
                    continue;
                }

                DefaultCookie cookie = this.InitCookie(header, nameBegin, nameEnd, valueBegin, valueEnd);
                if (cookie != null)
                {
                    cookies.Add(cookie);
                }
            }

            loop:
            return cookies;
        }
    }
}
