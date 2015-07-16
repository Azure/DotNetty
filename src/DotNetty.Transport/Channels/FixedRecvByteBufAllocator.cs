// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;

    /// <summary>
    /// The {@link RecvByteBufAllocator} that always yields the same buffer
    /// size prediction. This predictor ignores the feedback from the I/O thread.
    /// </summary>
    public sealed class FixedRecvByteBufAllocator : IRecvByteBufAllocator
    {
        public static readonly FixedRecvByteBufAllocator Default = new FixedRecvByteBufAllocator(4 * 1024);

        sealed class HandleImpl : IRecvByteBufAllocatorHandle
        {
            readonly int bufferSize;

            public HandleImpl(int bufferSize)
            {
                this.bufferSize = bufferSize;
            }

            public IByteBuffer Allocate(IByteBufferAllocator alloc)
            {
                return alloc.Buffer(this.bufferSize);
            }

            public int Guess()
            {
                return this.bufferSize;
            }

            public void Record(int actualReadBytes)
            {
            }
        }

        readonly IRecvByteBufAllocatorHandle handle;

        /// <summary>
        /// Creates a new predictor that always returns the same prediction of
        /// the specified buffer size.
        /// </summary>
        public FixedRecvByteBufAllocator(int bufferSize)
        {
            Contract.Requires(bufferSize > 0);

            this.handle = new HandleImpl(bufferSize);
        }

        public IRecvByteBufAllocatorHandle NewHandle()
        {
            return this.handle;
        }
    }
}