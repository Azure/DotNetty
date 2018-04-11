// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Cookies
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using System.Text;

    using static CookieUtil;

    // http://tools.ietf.org/html/rfc6265 compliant cookie encoder to be used server side,
    // so some fields are sent (Version is typically ignored).
    // 
    // As Netty's Cookie merges Expires and MaxAge into one single field, only Max-Age field is sent.
    // Note that multiple cookies must be sent as separate "Set-Cookie" headers.
    public sealed class ServerCookieEncoder : CookieEncoder
    {
        // 
        // Strict encoder that validates that name and value chars are in the valid scope
        // defined in RFC6265, and(for methods that accept multiple cookies) that only
        // one cookie is encoded with any given name. (If multiple cookies have the same
        // name, the last one is the one that is encoded.)
        // 
        public static readonly ServerCookieEncoder StrictEncoder = new ServerCookieEncoder(true);

        // 
        // Lax instance that doesn't validate name and value, and that allows multiple
        // cookies with the same name.
        // 
        public static readonly ServerCookieEncoder LaxEncoder = new ServerCookieEncoder(false);

        ServerCookieEncoder(bool strict) : base(strict)
        {
        }

        public string Encode(string name, string value) => this.Encode(new DefaultCookie(name, value));

        public string Encode(ICookie cookie)
        {
            Contract.Requires(cookie != null);

            string name = cookie.Name ?? nameof(cookie);
            string value = cookie.Value ?? "";

            this.ValidateCookie(name, value);

            StringBuilder buf = StringBuilder();

            if (cookie.Wrap)
            {
                AddQuoted(buf, name, value);
            }
            else
            {
                Add(buf, name, value);
            }

            if (cookie.MaxAge != long.MinValue)
            {
                Add(buf, (string)CookieHeaderNames.MaxAge, cookie.MaxAge);
                DateTime expires = DateTime.UtcNow.AddMilliseconds(cookie.MaxAge * 1000);
                buf.Append(CookieHeaderNames.Expires);
                buf.Append((char)HttpConstants.EqualsSign);
                DateFormatter.Append(expires, buf);
                buf.Append((char)HttpConstants.Semicolon);
                buf.Append((char)HttpConstants.HorizontalSpace);
            }

            if (cookie.Path != null)
            {
                Add(buf, (string)CookieHeaderNames.Path, cookie.Path);
            }

            if (cookie.Domain != null)
            {
                Add(buf, (string)CookieHeaderNames.Domain, cookie.Domain);
            }

            if (cookie.IsSecure)
            {
                Add(buf, (string)CookieHeaderNames.Secure);
            }

            if (cookie.IsHttpOnly)
            {
                Add(buf, (string)CookieHeaderNames.HttpOnly);
            }

            return StripTrailingSeparator(buf);
        }

        static List<string> Dedup(IReadOnlyList<string> encoded, IDictionary<string, int> nameToLastIndex)
        {
            var isLastInstance = new bool[encoded.Count];
            foreach (int idx in nameToLastIndex.Values)
            {
                isLastInstance[idx] = true;
            }

            var dedupd = new List<string>(nameToLastIndex.Count);
            for (int i = 0, n = encoded.Count; i < n; i++)
            {
                if (isLastInstance[i])
                {
                    dedupd.Add(encoded[i]);
                }
            }
            return dedupd;
        }

        public IList<string> Encode(params ICookie[] cookies)
        {
            if (cookies == null || cookies.Length == 0)
            {
                return ImmutableList<string>.Empty;
            }

            var encoded = new List<string>(cookies.Length);
            Dictionary<string, int> nameToIndex = this.Strict && cookies.Length > 1 ? new Dictionary<string, int>() : null;
            bool hasDupdName = false;
            for (int i = 0; i < cookies.Length; i++)
            {
                ICookie c = cookies[i];
                encoded.Add(this.Encode(c));
                if (nameToIndex != null)
                {
                    if (nameToIndex.ContainsKey(c.Name))
                    {
                        nameToIndex[c.Name] = i;
                        hasDupdName = true;
                    }
                    else
                    {
                        nameToIndex.Add(c.Name, i);
                    }
                }
            }
            return hasDupdName ? Dedup(encoded, nameToIndex) : encoded;
        }

        public IList<string> Encode(ICollection<ICookie> cookies)
        {
            Contract.Requires(cookies != null);
            if (cookies.Count == 0)
            {
                return ImmutableList<string>.Empty;
            }

            var encoded = new List<string>();
            var nameToIndex = new Dictionary<string, int>();
            bool hasDupdName = false;
            int i = 0;
            foreach (ICookie c in cookies)
            {
                encoded.Add(this.Encode(c));
                if (nameToIndex.ContainsKey(c.Name))
                {
                    nameToIndex[c.Name] = i;
                    hasDupdName = true;
                }
                else
                {
                    nameToIndex.Add(c.Name, i);
                }
                i++;
            }
            return hasDupdName ? Dedup(encoded, nameToIndex) : encoded;
        }
    }
}
