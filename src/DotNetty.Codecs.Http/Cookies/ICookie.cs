// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Cookies
{
    using System;

    // http://en.wikipedia.org/wiki/HTTP_cookie
    public interface ICookie : IEquatable<ICookie>, IComparable<ICookie>, IComparable
    {
        string Name { get; }

        string Value { get; set; }

        /// <summary>
        /// Returns true if the raw value of this {@link Cookie},
        /// was wrapped with double quotes in original Set-Cookie header.
        /// </summary>
        bool Wrap { get; set; }

        string Domain { get; set; }

        string Path { get; set; }

        long MaxAge { get; set; }

        bool IsSecure { get; set; }

        ///<summary>
        /// Checks to see if this Cookie can only be accessed via HTTP.
        /// If this returns true, the Cookie cannot be accessed through
        /// client side script - But only if the browser supports it.
        /// For more information, please look "http://www.owasp.org/index.php/HTTPOnly".
        ///</summary>
        bool IsHttpOnly { get; set; }
    }
}
