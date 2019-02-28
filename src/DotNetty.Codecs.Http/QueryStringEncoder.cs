// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Diagnostics.Contracts;
    using System.Text;

    /// <summary>
    /// Creates an URL-encoded URI from a path string and key-value parameter pairs.
    /// This encoder is for one time use only.  Create a new instance for each URI.
    /// 
    /// {@link QueryStringEncoder} encoder = new {@link QueryStringEncoder}("/hello");
    /// encoder.addParam("recipient", "world");
    /// assert encoder.toString().equals("/hello?recipient=world");
    /// </summary>
    public class QueryStringEncoder
    {
        readonly Encoding encoding;
        readonly StringBuilder uriBuilder;
        bool hasParams;

        public QueryStringEncoder(string uri) : this(uri, HttpConstants.DefaultEncoding)
        {
        }

        public QueryStringEncoder(string uri, Encoding encoding)
        {
            this.uriBuilder = new StringBuilder(uri);
            this.encoding = encoding;
        }

        public void AddParam(string name, string value)
        {
            Contract.Requires(name != null);
            if (this.hasParams)
            {
                this.uriBuilder.Append('&');
            }
            else
            {
                this.uriBuilder.Append('?');
                this.hasParams = true;
            }

            AppendComponent(name, this.encoding, this.uriBuilder);
            if (value != null)
            {
                this.uriBuilder.Append('=');
                AppendComponent(value, this.encoding, this.uriBuilder);
            }
        }

        public override string ToString() => this.uriBuilder.ToString();

        static void AppendComponent(string s, Encoding encoding, StringBuilder sb)
        {
            s = UrlEncoder.Encode(s, encoding);
            // replace all '+' with "%20"
            int idx = s.IndexOf('+');
            if (idx == -1)
            {
                sb.Append(s);
                return;
            }
            sb.Append(s, 0, idx).Append("%20");
            int size = s.Length;
            idx++;
            for (; idx < size; idx++)
            {
                char c = s[idx];
                if (c != '+')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append("%20");
                }
            }
        }
    }
}
