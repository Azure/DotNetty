// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Compression
{
    using System;

    static class ZlibUtil
    {
        public static void Fail(Inflater z, string message, int resultCode)
        {
            throw new DecompressionException($"{message} ({resultCode})" + (z.msg != null ? " : " + z.msg : ""));
        }

        public static void Fail(Deflater z, string message, int resultCode)
        {
            throw new CompressionException($"{message} ({resultCode})" + (z.msg != null ? " : " + z.msg : ""));
        }

        public static JZlib.WrapperType ConvertWrapperType(ZlibWrapper wrapper)
        {
            JZlib.WrapperType convertedWrapperType;
            switch (wrapper)
            {
                case ZlibWrapper.None:
                    convertedWrapperType = JZlib.W_NONE;
                    break;
                case ZlibWrapper.Zlib:
                    convertedWrapperType = JZlib.W_ZLIB;
                    break;
                case ZlibWrapper.Gzip:
                    convertedWrapperType = JZlib.W_GZIP;
                    break;
                case ZlibWrapper.ZlibOrNone:
                    convertedWrapperType = JZlib.W_ANY;
                    break;
                default:
                    throw new ArgumentException($"Unknown type {wrapper}");
            }

            return convertedWrapperType;
        }

        public static int WrapperOverhead(ZlibWrapper wrapper)
        {
            int overhead;
            switch (wrapper)
            {
                case ZlibWrapper.Zlib:
                    overhead = 2;
                    break;
                case ZlibWrapper.Gzip:
                    overhead = 10;
                    break;
                default:
                    throw new NotSupportedException($"Unknown value {wrapper}");
            }

            return overhead;
        }
    }
}
