// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Diagnostics.Contracts;

    /// <summary>
    ///     The <see cref="IRecvByteBufAllocator" /> that always yields the same buffer
    ///     size prediction. This predictor ignores the feedback from the I/O thread.
    /// </summary>
    public sealed class FixedRecvByteBufAllocator : DefaultMaxMessagesRecvByteBufAllocator
    {
        public static readonly FixedRecvByteBufAllocator Default = new FixedRecvByteBufAllocator(4 * 1024);

        sealed class HandleImpl : MaxMessageHandle<FixedRecvByteBufAllocator>
        {
            readonly int bufferSize;

            public HandleImpl(FixedRecvByteBufAllocator owner, int bufferSize)
                : base(owner)
            {
                this.bufferSize = bufferSize;
            }

            public override int Guess() => this.bufferSize;
        }

        readonly IRecvByteBufAllocatorHandle handle;

        /// <summary>
        ///     Creates a new predictor that always returns the same prediction of
        ///     the specified buffer size.
        /// </summary>
        public FixedRecvByteBufAllocator(int bufferSize)
        {
            Contract.Requires(bufferSize > 0);

            this.handle = new HandleImpl(this, bufferSize);
        }

        public override IRecvByteBufAllocatorHandle NewHandle() => this.handle;
    }
}