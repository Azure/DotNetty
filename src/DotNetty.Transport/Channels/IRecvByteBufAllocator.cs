// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    /// <summary>
    ///     Allocates a new receive buffer whose capacity is probably large enough to read all inbound data and small enough
    ///     not to waste its space.
    /// </summary>
    public interface IRecvByteBufAllocator
    {
        /// <summary>
        ///     Creates a new handle.  The handle provides the actual operations and keeps the internal information which is
        ///     required for predicting an optimal buffer capacity.
        /// </summary>
        IRecvByteBufAllocatorHandle NewHandle();
    }
}