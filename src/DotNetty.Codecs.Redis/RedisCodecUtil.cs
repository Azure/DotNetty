// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;
    using System.Globalization;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;

    static class RedisCodecUtil
    {
        internal const char RedisSimpleString = '+';
        internal const char RedisError = '-';
        internal const char RedisInteger = ':';
        internal const char RedisBulkString = '$';
        internal const char RedisArray = '*';

        internal static RedisMessageType ParseMessageType(byte byteCode)
        {
            switch ((char)byteCode)
            {
                case RedisSimpleString:
                    return RedisMessageType.SimpleString;
                case RedisError:
                    return RedisMessageType.Error;
                case RedisInteger:
                    return RedisMessageType.Integer;
                case RedisBulkString:
                    return RedisMessageType.BulkString;
                case RedisArray:
                    return RedisMessageType.ArrayHeader;
                default:
                    throw new RedisCodecException($"Unknown RedisMessageType code:{byteCode}");
            }
        }

        // todo: can optimize once ByteBufferUtil.WriteAscii is implemented
        internal static byte[] LongToAsciiBytes(long value) =>
            Encoding.ASCII.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));

        internal static short ToShort(char first, char second, ByteOrder byteOrder)
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

        internal static byte[] GetBytes(short value, ByteOrder byteOrder = ByteOrder.BigEndian)
        {
            switch (byteOrder)
            {
                case ByteOrder.BigEndian:
                    return new[]
                    {
                        (byte)(value & 0xff),
                        (byte)((value >> 8) & 0xff)
                    };
                case ByteOrder.LittleEndian:
                    return new[]
                    {
                        (byte)((value >> 8) & 0xff),
                        (byte)(value & 0xff)
                    };
                default:
                    throw new InvalidOperationException($"Unknown ByteOrder type {byteOrder}");
            }
        }
    }
}