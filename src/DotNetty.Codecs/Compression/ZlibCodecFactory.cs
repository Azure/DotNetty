// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Compression
{
    public static class ZlibCodecFactory
    {
        public static ZlibEncoder NewZlibEncoder(int compressionLevel) => new JZlibEncoder(compressionLevel);

        public static ZlibEncoder NewZlibEncoder(ZlibWrapper wrapper) => new JZlibEncoder(wrapper);

        public static ZlibEncoder NewZlibEncoder(ZlibWrapper wrapper, int compressionLevel) => new JZlibEncoder(wrapper, compressionLevel);

        public static ZlibEncoder NewZlibEncoder(ZlibWrapper wrapper, int compressionLevel, int windowBits, int memLevel) => 
            new JZlibEncoder(wrapper, compressionLevel, windowBits, memLevel);

        public static ZlibEncoder NewZlibEncoder(byte[] dictionary) => new JZlibEncoder(dictionary);

        public static ZlibEncoder NewZlibEncoder(int compressionLevel, byte[] dictionary) => new JZlibEncoder(compressionLevel, dictionary);

        public static ZlibEncoder NewZlibEncoder(int compressionLevel, int windowBits, int memLevel, byte[] dictionary) => 
            new JZlibEncoder(compressionLevel, windowBits, memLevel, dictionary);

        public static ZlibDecoder NewZlibDecoder() => new JZlibDecoder();

        public static ZlibDecoder NewZlibDecoder(ZlibWrapper wrapper) => new JZlibDecoder(wrapper);

        public static ZlibDecoder NewZlibDecoder(byte[] dictionary) => new JZlibDecoder(dictionary);
    }
}
