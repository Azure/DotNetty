// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using DotNetty.Common;

    public interface IByteBufferHolder : IReferenceCounted
    {
        /// <summary>
        /// Return the data which is held by this {@link ByteBufHolder}.
        /// </summary>
        IByteBuffer Content { get; }

        /// <summary>
        /// Create a deep copy of this {@link ByteBufHolder}.
        /// </summary>
        IByteBufferHolder Copy();

        /// <summary>
        /// Duplicate the {@link ByteBufHolder}. Be aware that this will not automatically call {@link #retain()}.
        /// </summary>
        IByteBufferHolder Duplicate();

        //IByteBufferHolder Retain();

        //IByteBufferHolder Retain(int increment);

        //IByteBufferHolder touch();

        //IByteBufferHolder touch(object hint);
    }
}