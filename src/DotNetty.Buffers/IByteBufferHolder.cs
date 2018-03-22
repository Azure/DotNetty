// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using DotNetty.Common;

    public interface IByteBufferHolder : IReferenceCounted
    {
        /// <summary>
        ///     Return the data which is held by this {@link ByteBufHolder}.
        /// </summary>
        IByteBuffer Content { get; }

        /// <summary>
        ///     Create a deep copy of this {@link ByteBufHolder}.
        /// </summary>
        IByteBufferHolder Copy();

        /// <summary>
        ///     Duplicate the {@link ByteBufHolder}. Be aware that this will not automatically call {@link #retain()}.
        /// </summary>
        IByteBufferHolder Duplicate();

        /// <summary>
        ///     Duplicates this {@link ByteBufHolder}. This method returns a retained duplicate unlike {@link #duplicate()}.
        /// </summary>
        IByteBufferHolder RetainedDuplicate();

        /// <summary>
        ///    Returns a new {@link ByteBufHolder} which contains the specified {@code content}.
        /// </summary>
        IByteBufferHolder Replace(IByteBuffer content);
    }
}