// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Compression
{
    public abstract class ZlibDecoder : ByteToMessageDecoder
    {
        public abstract bool IsClosed { get; }
    }
}
