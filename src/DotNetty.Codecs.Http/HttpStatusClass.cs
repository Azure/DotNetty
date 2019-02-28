// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System;
    using DotNetty.Common.Utilities;

    public struct HttpStatusClass : IEquatable<HttpStatusClass>
    {
        public static readonly HttpStatusClass Informational = new HttpStatusClass(100, 200, "Informational");

        public static readonly HttpStatusClass Success = new HttpStatusClass(200, 300, "Success");

        public static readonly HttpStatusClass Redirection = new HttpStatusClass(300, 400, "Redirection");

        public static readonly HttpStatusClass ClientError = new HttpStatusClass(400, 500, "Client Error");

        public static readonly HttpStatusClass ServerError = new HttpStatusClass(500, 600, "Server Error");

        public static readonly HttpStatusClass Unknown = new HttpStatusClass(0, 0, "Unknown Status");

        public static HttpStatusClass ValueOf(int code)
        {
            if (Contains(Informational, code))
            {
                return Informational;
            }
            if (Contains(Success, code))
            {
                return Success;
            }
            if (Contains(Redirection, code))
            {
                return Redirection;
            }
            if (Contains(ClientError, code))
            {
                return ClientError;
            }
            if (Contains(ServerError, code))
            {
                return ServerError;
            }
            return Unknown;
        }

        public static HttpStatusClass ValueOf(ICharSequence code)
        {
            if (code != null && code.Count == 3)
            {
                char c0 = code[0];
                return IsDigit(c0) && IsDigit(code[1]) && IsDigit(code[2]) 
                    ? ValueOf(Digit(c0) * 100)
                    : Unknown;
            }

            return Unknown;
        }

        static int Digit(char c) => c - '0';

        static bool IsDigit(char c) => c >= '0' && c <= '9';

        readonly int min;
        readonly int max;
        readonly AsciiString defaultReasonPhrase;

        HttpStatusClass(int min, int max, string defaultReasonPhrase)
        {
            this.min = min;
            this.max = max;
            this.defaultReasonPhrase = AsciiString.Cached(defaultReasonPhrase);
        }

        public bool Contains(int code) => Contains(this, code);

        public static bool Contains(HttpStatusClass httpStatusClass, int code)
        {
            if ((httpStatusClass.min & httpStatusClass.max) == 0)
            {
                return code < 100 || code >= 600;
            }

            return code >= httpStatusClass.min && code < httpStatusClass.max;
        }

        public AsciiString DefaultReasonPhrase => this.defaultReasonPhrase;

        public bool Equals(HttpStatusClass other) => this.min == other.min && this.max == other.max;

        public override bool Equals(object obj) =>  obj is HttpStatusClass && this.Equals((HttpStatusClass)obj);

        public override int GetHashCode() => this.min.GetHashCode() ^ this.max.GetHashCode();

        public static bool operator !=(HttpStatusClass left, HttpStatusClass right) => !(left == right);

        public static bool operator ==(HttpStatusClass left, HttpStatusClass right) => left.Equals(right);
    }
}
