// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;

    public interface IRecvByteBufAllocatorHandle
    {
        /// <summary>
        ///     Creates a new receive buffer whose capacity is probably large enough to read all inbound data and small
        ///     enough not to waste its space.
        /// </summary>
        IByteBuffer Allocate(IByteBufferAllocator alloc);

        /// <summary>
        ///     Similar to <see cref="Allocate" /> except that it does not allocate anything but just tells the
        ///     capacity.
        /// </summary>
        int Guess();

        /// <summary>
        ///     Reset any counters that have accumulated and recommend how many messages/bytes should be read for the next
        ///     read loop.
        ///     <p>
        ///         This may be used by <see cref="ContinueReading" /> to determine if the read operation should complete.
        ///     </p>
        ///     This is only ever a hint and may be ignored by the implementation.
        /// </summary>
        /// <param name="config">The channel configuration which may impact this object's behavior.</param>
        void Reset(IChannelConfiguration config);

        /// <summary>Increment the number of messages that have been read for the current read loop.</summary>
        /// <param name="numMessages">The amount to increment by.</param>
        void IncMessagesRead(int numMessages);

        /// <summary>
        ///     Get or set the bytes that have been read for the last read operation.
        ///     This may be used to increment the number of bytes that have been read.
        /// </summary>
        /// <remarks>
        ///     Returned value may be negative if an read error
        ///     occurs. If a negative value is seen it is expected to be return on the next set to
        ///     <see cref="LastBytesRead" />. A negative value will signal a termination condition enforced externally
        ///     to this class and is not required to be enforced in <see cref="ContinueReading" />.
        /// </remarks>
        int LastBytesRead { get; set; }

        /// <summary>Get or set how many bytes the read operation will (or did) attempt to read.</summary>
        int AttemptedBytesRead { get; set; }

        /// <summary>Determine if the current read loop should should continue.</summary>
        /// <returns><c>true</c> if the read loop should continue reading. <c>false</c> if the read loop is complete.</returns>
        bool ContinueReading();

        /// <summary>Signals read completion.</summary>
        void ReadComplete();
    }
}