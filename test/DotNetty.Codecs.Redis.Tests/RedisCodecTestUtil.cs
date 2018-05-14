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
        internal static byte[] BytesOf(long value) => Encoding.ASCII.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));

        internal static byte[] BytesOf(string value) => Encoding.UTF8.GetBytes(value);

        internal static byte[] BytesOf(IByteBuffer byteBuffer)
        {
            var data = new byte[byteBuffer.ReadableBytes];
            byteBuffer.ReadBytes(data);
            return data;
        }

        internal static string StringOf(IByteBuffer buf) => Encoding.UTF8.GetString(BytesOf(buf));

        internal static IByteBuffer ByteBufOf(string s) => ByteBufOf(BytesOf(s));

        internal static IByteBuffer ByteBufOf(byte[] data) => Unpooled.WrappedBuffer(data);
    }
}
