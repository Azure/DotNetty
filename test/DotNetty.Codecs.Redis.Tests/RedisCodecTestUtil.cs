// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Tests
{
    using System;
    using System.Globalization;
    using System.Text;
    using DotNetty.Buffers;

    static class RedisCodecTestUtil
    {
        internal static byte[] Bytes(this IByteBuffer byteBuffer)
        {
            var data = new byte[byteBuffer.ReadableBytes];
            byteBuffer.ReadBytes(data);

            return data;
        }
        internal static byte[] Bytes(this long value) => 
            Encoding.ASCII.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));

        internal static IByteBuffer Buffer(this long value) => 
            Buffer(value.Bytes());

        internal static IByteBuffer Buffer(this string value) => 
            Buffer(Bytes(value));

        internal static byte[] Bytes(this string value) => 
            Encoding.UTF8.GetBytes(value);

        internal static IByteBuffer Buffer(this byte[] data) => 
            Unpooled.WrappedBuffer(data);
    }
}
