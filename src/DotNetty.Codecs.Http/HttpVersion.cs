// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public class HttpVersion : IComparable<HttpVersion>, IComparable
    {
        static readonly Regex VersionPattern = new Regex("^(\\S+)/(\\d+)\\.(\\d+)$", RegexOptions.Compiled);

        internal static readonly AsciiString Http10String = new AsciiString("HTTP/1.0");
        internal static readonly AsciiString Http11String = new AsciiString("HTTP/1.1");

        public static readonly HttpVersion Http10 = new HttpVersion("HTTP", 1, 0, false, true);
        public static readonly HttpVersion Http11 = new HttpVersion("HTTP", 1, 1, true, true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static HttpVersion ValueOf(AsciiString text)
        {
            if (text == null)
            {
                ThrowHelper.ThrowArgumentException_NullText();
            }

            // ReSharper disable once PossibleNullReferenceException
            HttpVersion version = ValueOfInline(text.Array);
            if (version != null)
            {
                return version;
            }

            // Fall back to slow path
            text = text.Trim();

            if (text.Count == 0)
            {
                ThrowHelper.ThrowArgumentException_EmptyText();
            }

            // Try to match without convert to uppercase first as this is what 99% of all clients
            // will send anyway. Also there is a change to the RFC to make it clear that it is
            // expected to be case-sensitive
            //
            // See:
            // * http://trac.tools.ietf.org/wg/httpbis/trac/ticket/1
            // * http://trac.tools.ietf.org/wg/httpbis/trac/wiki
            //
            return Version0(text) ?? new HttpVersion(text.ToString(), true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static HttpVersion ValueOfInline(byte[] bytes)
        {
            if (bytes.Length != 8) return null;

            if (bytes[0] != (byte)'H') return null;
            if (bytes[1] != (byte)'T') return null;
            if (bytes[2] != (byte)'T') return null;
            if (bytes[3] != (byte)'P') return null;
            if (bytes[4] != (byte)'/') return null;
            if (bytes[5] != (byte)'1') return null;
            if (bytes[6] != (byte)'.') return null;
            switch (bytes[7])
            {
                case (byte)'1':
                    return Http11;
                case (byte)'0':
                    return Http10;
                default:
                    return null;
            }
        }

        static HttpVersion Version0(AsciiString text)
        {
            if (Http11String.Equals(text))
            {
                return Http11;
            }
            if (Http10String.Equals(text))
            {
                return Http10;
            }

            return null;
        }

        readonly string protocolName;
        readonly int majorVersion;
        readonly int minorVersion;
        readonly AsciiString text;
        readonly bool keepAliveDefault;
        readonly byte[] bytes;

        public HttpVersion(string text, bool keepAliveDefault)
        {
            Contract.Requires(text != null);

            text = text.Trim().ToUpper();
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("empty text");
            }

            Match match = VersionPattern.Match(text);
            if (!match.Success)
            {
                throw new ArgumentException($"invalid version format: {text}");
            }

            this.protocolName = match.Groups[1].Value;
            this.majorVersion = int.Parse(match.Groups[2].Value);
            this.minorVersion = int.Parse(match.Groups[3].Value);
            this.text = new AsciiString($"{this.ProtocolName}/{this.MajorVersion}.{this.MinorVersion}");
            this.keepAliveDefault = keepAliveDefault;
            this.bytes = null;
        }

        HttpVersion(string protocolName, int majorVersion, int minorVersion, bool keepAliveDefault, bool bytes)
        {
            if (protocolName == null)
            {
                throw new ArgumentException(nameof(protocolName));
            }

            protocolName = protocolName.Trim().ToUpper();
            if (string.IsNullOrEmpty(protocolName))
            {
                throw new ArgumentException("empty protocolName");
            }

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < protocolName.Length; i++)
            {
                char c = protocolName[i];
                if (CharUtil.IsISOControl(c) || char.IsWhiteSpace(c))
                {
                    throw new ArgumentException($"invalid character {c} in protocolName");
                }
            }

            if (majorVersion < 0)
            {
                throw new ArgumentException("negative majorVersion");
            }
            if (minorVersion < 0)
            {
                throw new ArgumentException("negative minorVersion");
            }

            this.protocolName = protocolName;
            this.majorVersion = majorVersion;
            this.minorVersion = minorVersion;
            this.text = new AsciiString(protocolName + '/' + majorVersion + '.' + minorVersion);
            this.keepAliveDefault = keepAliveDefault;

            this.bytes = bytes ? this.text.Array : null;
        }

        public string ProtocolName => this.protocolName;

        public int MajorVersion => this.majorVersion;

        public int MinorVersion => this.minorVersion;

        public AsciiString Text => this.text;

        public bool IsKeepAliveDefault => this.keepAliveDefault;

        public override string ToString() => this.text.ToString();

        public override int GetHashCode() => (this.protocolName.GetHashCode() * 31 + this.majorVersion) * 31 + this.minorVersion;

        public override bool Equals(object obj)
        {
            if (!(obj is HttpVersion that))
            {
                return false;
            }

            return this.minorVersion == that.minorVersion
                && this.majorVersion == that.majorVersion
                && this.protocolName.Equals(that.protocolName);
        }

        public int CompareTo(HttpVersion other)
        {
            int v = string.CompareOrdinal(this.protocolName, other.protocolName);
            if (v != 0)
            {
                return v;
            }

            v = this.majorVersion - other.majorVersion;
            if (v != 0)
            {
                return v;
            }

            return this.minorVersion - other.minorVersion;
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return 0;
            }

            if (!(obj is HttpVersion))
            {
                throw new ArgumentException($"{nameof(obj)} must be of {nameof(HttpVersion)} type");
            }

            return this.CompareTo((HttpVersion)obj);
        }

        internal void Encode(IByteBuffer buf)
        {
            if (this.bytes == null)
            {
                buf.WriteCharSequence(this.text, Encoding.ASCII);
            }
            else
            {
                buf.WriteBytes(this.bytes);
            }
        }
    }
}
