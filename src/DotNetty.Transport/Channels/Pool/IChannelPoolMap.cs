// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    /// <summary>
    /// Allows the mapping of <see cref="IChannelPool"/> implementations to a specific key.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TPool">The type of the <see cref="IChannelPool"/>.</typeparam>
    public interface IChannelPoolMap<TKey, TPool>
        where TPool : IChannelPool
    {
        /// <summary>
        /// Returns the <see cref="IChannelPool"/> for the <paramref name="key"/>. This will never return <c>null</c>,
        /// but create a new <see cref="IChannelPool"/> if non exists for they requested <paramref name="key"/>.
        /// Please note that <c>null</c> keys are not allowed.
        /// </summary>
        /// <param name="key">The key for the desired <see cref="IChannelPool"/></param>
        /// <returns>The <see cref="IChannelPool"/> for the specified <paramref name="key"/>.</returns>
        TPool Get(TKey key);

        /// <summary>
        /// Checks whether the <see cref="IChannelPoolMap{TKey,TPool}"/> contains an <see cref="IChannelPool"/> for the
        /// given <paramref name="key"/>. Please note that <c>null</c> keys are not allowed.
        /// </summary>
        /// <param name="key">The key to search the <see cref="IChannelPoolMap{TKey,TPool}"/> for.</param>
        /// <returns><c>true</c> if a <see cref="IChannelPool"/> exists for the given <paramref name="key"/>, otherwise <c>false</c>.</returns>
        bool Contains(TKey key);
    }
}