// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Tests.Protobuf
{
    using DotNetty.Buffers;

    static class TestUtil
    {
        internal static byte[] GetReadableBytes(IByteBuffer byteBuffer)
        {
            var data = new byte[byteBuffer.ReadableBytes];
            byteBuffer.ReadBytes(data);

            return data;
        }
    }
}
