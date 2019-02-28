// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//JZlib 0.0.* were released under the GNU LGPL license.Later, we have switched
//over to a BSD-style license. 

//------------------------------------------------------------------------------
//Copyright (c) 2000-2011 ymnk, JCraft, Inc.All rights reserved.

//Redistribution and use in source and binary forms, with or without
//modification, are permitted provided that the following conditions are met:

//  1. Redistributions of source code must retain the above copyright notice,
//     this list of conditions and the following disclaimer.

//  2. Redistributions in binary form must reproduce the above copyright
//     notice, this list of conditions and the following disclaimer in 
//     the documentation and/or other materials provided with the distribution.

//  3. The names of the authors may not be used to endorse or promote products
//     derived from this software without specific prior written permission.

//THIS SOFTWARE IS PROVIDED ``AS IS'' AND ANY EXPRESSED OR IMPLIED WARRANTIES,
//INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
//FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.IN NO EVENT SHALL JCRAFT,
//INC.OR ANY CONTRIBUTORS TO THIS SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT,
//INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
//LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
//OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT(INCLUDING
//NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
//EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// ReSharper disable ArrangeThisQualifier
// ReSharper disable InconsistentNaming
namespace DotNetty.Codecs.Compression
{
    /// <summary>
    /// https://github.com/ymnk/jzlib/blob/master/src/main/java/com/jcraft/jzlib/JZlib.java
    /// </summary>
    public sealed class JZlib
    {
        const string VersionString = "1.1.0";
        public static string Version() => VersionString;

        public static readonly int MAX_WBITS = 15; // 32K LZ77 window
        public static readonly int DEF_WBITS = MAX_WBITS;

        public enum WrapperType
        {
            NONE,
            ZLIB,
            GZIP,
            ANY
        }

        public static readonly WrapperType W_NONE = WrapperType.NONE;
        public static readonly WrapperType W_ZLIB = WrapperType.ZLIB;
        public static readonly WrapperType W_GZIP = WrapperType.GZIP;
        public static readonly WrapperType W_ANY = WrapperType.ANY;

        // compression levels
        public static readonly int Z_NO_COMPRESSION = 0;
        public static readonly int Z_BEST_SPEED = 1;
        public static readonly int Z_BEST_COMPRESSION = 9;
        public static readonly int Z_DEFAULT_COMPRESSION = (-1);

        // compression strategy
        public static readonly int Z_FILTERED = 1;
        public static readonly int Z_HUFFMAN_ONLY = 2;
        public static readonly int Z_DEFAULT_STRATEGY = 0;

        public static readonly int Z_NO_FLUSH = 0;
        public static readonly int Z_PARTIAL_FLUSH = 1;
        public static readonly int Z_SYNC_FLUSH = 2;
        public static readonly int Z_FULL_FLUSH = 3;
        public static readonly int Z_FINISH = 4;

        public static readonly int Z_OK = 0;
        public static readonly int Z_STREAM_END = 1;
        public static readonly int Z_NEED_DICT = 2;
        public static readonly int Z_ERRNO = -1;
        public static readonly int Z_STREAM_ERROR = -2;
        public static readonly int Z_DATA_ERROR = -3;
        public static readonly int Z_MEM_ERROR = -4;
        public static readonly int Z_BUF_ERROR = -5;
        public static readonly int Z_VERSION_ERROR = -6;

        // The three kinds of block type
        public static readonly byte Z_BINARY = 0;
        public static readonly byte Z_ASCII = 1;
        public static readonly byte Z_UNKNOWN = 2;

        public static long Adler32_combine(long adler1, long adler2, long len2) =>
            Adler32.Combine(adler1, adler2, len2);
    }
}
