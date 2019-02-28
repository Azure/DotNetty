// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System;
    using DotNetty.Buffers;

    static class RedisConstants
    {
        static readonly ByteOrder DefaultByteOrder = BitConverter.IsLittleEndian ? ByteOrder.LittleEndian : ByteOrder.BigEndian;

        internal static readonly int MaximumInlineMessageLength = 1024 * 64;

        internal static readonly int TypeLength = 1;

        internal static readonly int EndOfLineLength = 2;

        internal static readonly int NullLength = 2;

        internal static readonly int NullValue = -1;

        internal static readonly int RedisMessageMaxLength = 512 * 1024 * 1024; // 512MB

        // 64KB is max inline length of current Redis server implementation.
        internal static readonly int RedisInlineMessageMaxLength = 64 * 1024;

        internal static readonly int PositiveLongMaxLength = 19; // length of Long.MAX_VALUE

        internal static readonly int LongMaxLength = PositiveLongMaxLength + 1; // +1 is sign

        internal static readonly short NullShort = RedisCodecUtil.MakeShort('-', '1', DefaultByteOrder);

        internal static readonly short EndOfLineShort = RedisCodecUtil.MakeShort('\r', '\n', DefaultByteOrder);
    }
}