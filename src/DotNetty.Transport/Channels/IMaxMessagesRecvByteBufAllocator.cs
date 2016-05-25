// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    /// <summary>
    ///     <see cref="IRecvByteBufAllocator" /> that limits the number of read operations that will be attempted when a read
    ///     operation
    ///     is attempted by the event loop.
    /// </summary>
    public interface IMaxMessagesRecvByteBufAllocator : IRecvByteBufAllocator
    {
        /// <summary>
        ///     Gets or sets the maximum number of messages to read per read loop.
        ///     If this value is greater than 1, an event loop might attempt to read multiple times to procure multiple messages.
        /// </summary>
        int MaxMessagesPerRead { get; set; }
    }
}