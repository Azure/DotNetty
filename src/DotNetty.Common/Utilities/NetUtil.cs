// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;

    public static class NetUtil
    {
        public static string ToSocketAddressString(string host, int port)
        {
            string portStr = Convert.ToString(port);
            return NewSocketAddressStringBuilder(host, portStr,
                !IsValidIpV6Address(host)).Append(':').Append(portStr).ToString();
        }

        static StringBuilder NewSocketAddressStringBuilder(string host, string port, bool ipv4)
        {
            int hostLen = host.Length;
            if (ipv4)
            {
                // Need to include enough space for hostString:port.
                return new StringBuilder(hostLen + 1 + port.Length).Append(host);
            }

            // Need to include enough space for [hostString]:port.
            var stringBuilder = new StringBuilder(hostLen + 3 + port.Length);
            if (hostLen > 1 && host[0] == '[' && host[hostLen - 1] == ']')
            {
                return stringBuilder.Append(host);
            }

            return stringBuilder.Append('[').Append(host).Append(']');
        }

        public static bool IsValidIpV6Address(string ip)
        {
            int end = ip.Length;
            if (end < 2)
            {
                return false;
            }

            // strip "[]"
            int start;
            char c = ip[0];
            if (c == '[')
            {
                end--;
                if (ip[end] != ']')
                {
                    // must have a close ]
                    return false;
                }

                start = 1;
                c = ip[1];
            }
            else
            {
                start = 0;
            }

            int colons;
            int compressBegin;
            if (c == ':')
            {
                // an IPv6 address can start with "::" or with a number
                if (ip[start + 1] != ':')
                {
                    return false;
                }

                colons = 2;
                compressBegin = start;
                start += 2;
            }
            else
            {
                colons = 0;
                compressBegin = -1;
            }

            int wordLen = 0;
            for (int i = start; i < end; i++)
            {
                c = ip[i];
                if (IsValidHexChar(c))
                {
                    if (wordLen < 4)
                    {
                        wordLen++;
                        continue;
                    }

                    return false;
                }

                switch (c)
                {
                    case ':':
                        if (colons > 7)
                        {
                            return false;
                        }

                        if (ip[i - 1] == ':')
                        {
                            if (compressBegin >= 0)
                            {
                                return false;
                            }

                            compressBegin = i - 1;
                        }
                        else
                        {
                            wordLen = 0;
                        }

                        colons++;
                        break;
                    case '.':
                        // case for the last 32-bits represented as IPv4 x:x:x:x:x:x:d.d.d.d

                        // check a normal case (6 single colons)
                        if (compressBegin < 0 && colons != 6 ||
                            // a special case ::1:2:3:4:5:d.d.d.d allows 7 colons with an
                            // IPv4 ending, otherwise 7 :'s is bad
                            (colons == 7 && compressBegin >= start || colons > 7))
                        {
                            return false;
                        }

                        // Verify this address is of the correct structure to contain an IPv4 address.
                        // It must be IPv4-Mapped or IPv4-Compatible
                        // (see https://tools.ietf.org/html/rfc4291#section-2.5.5).
                        int ipv4Start = i - wordLen;
                        int j = ipv4Start - 2; // index of character before the previous ':'.
                        if (IsValidIPv4MappedChar(ip[j]))
                        {
                            if (!IsValidIPv4MappedChar(ip[j - 1]) ||
                                !IsValidIPv4MappedChar(ip[j - 2]) ||
                                !IsValidIPv4MappedChar(ip[j - 3]))
                            {
                                return false;
                            }

                            j -= 5;
                        }

                        for (; j >= start; --j)
                        {
                            char tmpChar = ip[j];
                            if (tmpChar != '0' && tmpChar != ':')
                            {
                                return false;
                            }
                        }

                        // 7 - is minimum IPv4 address length
                        int ipv4End = ip.IndexOf('%', ipv4Start + 7);
                        if (ipv4End < 0)
                        {
                            ipv4End = end;
                        }

                        return IsValidIpV4Address(ip, ipv4Start, ipv4End);
                    case '%':
                        // strip the interface name/index after the percent sign
                        end = i;
                        goto loop;
                    default:
                        return false;
                }

                loop:
                // normal case without compression
                if (compressBegin < 0)
                {
                    return colons == 7 && wordLen > 0;
                }

                return compressBegin + 2 == end ||
                    // 8 colons is valid only if compression in start or end
                    wordLen > 0 && (colons < 8 || compressBegin <= start);
            }

            // normal case without compression
            if (compressBegin < 0)
            {
                return colons == 7 && wordLen > 0;
            }

            return compressBegin + 2 == end ||
                // 8 colons is valid only if compression in start or end
                wordLen > 0 && (colons < 8 || compressBegin <= start);
        }

        static bool IsValidIpV4Address(string ip, int from, int toExcluded)
        {
            int len = toExcluded - from;
            int i;
            return len <= 15 && len >= 7 &&
                (i = ip.IndexOf('.', from + 1)) > 0 && IsValidIpV4Word(ip, from, i) &&
                (i = ip.IndexOf('.', from = i + 2)) > 0 && IsValidIpV4Word(ip, from - 1, i) &&
                (i = ip.IndexOf('.', from = i + 2)) > 0 && IsValidIpV4Word(ip, from - 1, i) &&
                IsValidIpV4Word(ip, i + 1, toExcluded);
        }

        static bool IsValidIpV4Word(string word, int from, int toExclusive)
        {
            int len = toExclusive - from;
            char c0, c1, c2;
            if (len < 1 || len > 3 || (c0 = word[from]) < '0')
            {
                return false;
            }

            if (len == 3)
            {
                return (c1 = word[from + 1]) >= '0' 
                    && (c2 = word[from + 2]) >= '0' 
                    && (c0 <= '1' && c1 <= '9' && c2 <= '9' 
                        || c0 == '2' && c1 <= '5' && (c2 <= '5' || c1 < '5' && c2 <= '9'));
            }

            return c0 <= '9' && (len == 1 || IsValidNumericChar(word[from + 1]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsValidHexChar(char c)
        {
            return c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsValidNumericChar(char c)
        {
            return c >= '0' && c <= '9';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsValidIPv4MappedChar(char c)
        {
            return c == 'f' || c == 'F';
        }
    }
}
