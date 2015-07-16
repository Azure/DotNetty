// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    public interface IMessageSizeEstimatorHandle
    {
        /// <summary>
        /// Calculate the size of the given message.
        ///
        /// @param msg       The message for which the size should be calculated
        /// @return size     The size in bytes. The returned size must be >= 0
        /// </summary>
        int Size(object msg);
    }
}