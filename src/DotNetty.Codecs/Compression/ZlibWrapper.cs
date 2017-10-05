// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Compression
{
    /**
     * The container file formats that wrap the stream compressed by the DEFLATE
     * algorithm.
     */
    public enum ZlibWrapper
    {
        /**
         * The ZLIB wrapper as specified in <a href="http://tools.ietf.org/html/rfc1950">RFC 1950</a>.
         */
        Zlib,
        /**
         * The GZIP wrapper as specified in <a href="http://tools.ietf.org/html/rfc1952">RFC 1952</a>.
         */
        Gzip,
        /**
         * Raw DEFLATE stream only (no header and no footer).
         */
        None,
        /**
         * Try {@link #ZLIB} first and then {@link #NONE} if the first attempt fails.
         * Please note that you can specify this wrapper type only when decompressing.
         */
        ZlibOrNone
    }
}
