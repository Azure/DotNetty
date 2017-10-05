// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable NotResolvedInText
namespace DotNetty.Codecs.Http
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NullText()
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException("text");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_EmptyText()
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException("text is empty (possibly HTTP/0.9)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderName()
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException("empty headers are not allowed", "name");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_TrailingHeaderName(ICharSequence name)
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException(string.Format("prohibited trailing header: {0}", name));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderValue(byte value)
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException(string.Format("a header name cannot contain the following prohibited characters: =,;: \\t\\r\\n\\v\\f: {0}", value));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderValue(char value)
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException(string.Format("a header name cannot contain the following prohibited characters: =,;: \\t\\r\\n\\v\\f: {0}", value));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderValueNonAscii(byte value)
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException(string.Format("a header name cannot contain non-ASCII character: {0}", value));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderValueNonAscii(char value)
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException(string.Format("a header name cannot contain non-ASCII character: {0}", value));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderValueEnd(ICharSequence seq)
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException(string.Format("a header value must not end with '\\r' or '\\n':{0}", seq));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderValueNullChar()
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException("a header value contains a prohibited character '\0'");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderValueVerticalTabChar()
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException("a header value contains a prohibited character '\\v'");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeaderValueFormFeed()
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException("a header value contains a prohibited character '\\f'");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NewLineAfterLineFeed()
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException("only '\\n' is allowed after '\\r'");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_TabAndSpaceAfterLineFeed()
        {
            throw GetArgumentException();

            ArgumentException GetArgumentException()
            {
                return new ArgumentException("only ' ' and '\\t' are allowed after '\\n'");
            }
        }
    }
}
