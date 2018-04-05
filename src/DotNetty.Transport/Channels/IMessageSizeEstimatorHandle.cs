// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    public interface IMessageSizeEstimatorHandle
    {
        /// <summary>
        /// Calculates the size of the given message.
        /// </summary>
        /// <param name="msg">The message for which the size should be calculated.</param>
        /// <returns>The size in bytes. The returned size must be >= 0</returns>
        int Size(object msg);
    }
}