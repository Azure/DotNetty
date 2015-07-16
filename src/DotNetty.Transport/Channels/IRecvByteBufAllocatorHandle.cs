// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Buffers;

    public interface IRecvByteBufAllocatorHandle
    {
        /// <summary>
        /// Creates a new receive buffer whose capacity is probably large enough to read all inbound data and small
        /// enough not to waste its space.
        /// </summary>
        IByteBuffer Allocate(IByteBufferAllocator alloc);

        /// <summary>
        /// Similar to {@link #allocate(ByteBufAllocator)} except that it does not allocate anything but just tells the
        /// capacity.
        /// </summary>
        int Guess();

        /// <summary>
        /// Records the the actual number of read bytes in the previous read operation so that the allocator allocates
        /// the buffer with potentially more correct capacity.
        ///
        /// @param actualReadBytes the actual number of read bytes in the previous read operation
        /// </summary>
        void Record(int actualReadBytes);
    }
}