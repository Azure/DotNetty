// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System.Threading.Tasks;

    /// <summary>
    /// Called before an <see cref="IChannel"/> will be returned via <see cref="IChannelPool.AcquireAsync"/>.
    /// </summary>
    public interface IChannelHealthChecker
    {
        /// <summary>
        /// Checks if the given channel is healthy (which means it can be used). This method will be called by the
        /// <see cref="IEventLoop"/> of the given <see cref="IChannel"/>
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> to check for healthiness.</param>
        /// <returns><c>true</c> if the given <see cref="IChannel"/> is healthy, otherwise <c>false</c>.</returns>
        ValueTask<bool> IsHealthyAsync(IChannel channel);
    }
}