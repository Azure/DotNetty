// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;
    using System.Globalization;
    using System.Text;
    using DotNetty.Buffers;

    static class RedisCodecUtil
    {
        internal static byte[] LongToAsciiBytes(long value) =>
            Encoding.ASCII.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));

        internal static short MakeShort(char first, char second, ByteOrder byteOrder)
        {
            switch (byteOrder)
            {
                case ByteOrder.BigEndian:
                    return (short)((second << 8) | first);
                case ByteOrder.LittleEndian:
                    return (short)((first << 8) | second);
                default:
                    throw new InvalidOperationException($"Unknown ByteOrder type {byteOrder}");
            }
        }

        internal static byte[] ShortToBytes(short value, ByteOrder byteOrder = ByteOrder.BigEndian)
        {
            var bytes = new byte[2];
            switch (byteOrder)
            {
                case ByteOrder.BigEndian:
                    bytes[1] = (byte)((value >> 8) & 0xff);
                    bytes[0] = (byte)(value & 0xff);
                    break;
                case ByteOrder.LittleEndian:
                    bytes[0] = (byte)((value >> 8) & 0xff);
                    bytes[1] = (byte)(value & 0xff);
                    break;
            }
            return bytes;
        }
    }
}