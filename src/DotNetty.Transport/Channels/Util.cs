// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;

    static class Util
    {
        static readonly IInternalLogger Log = InternalLoggerFactory.GetInstance<IChannel>();

        /// <summary>
        ///     Marks the specified {@code promise} as success.  If the {@code promise} is done already, log a message.
        /// </summary>
        public static void SafeSetSuccess(TaskCompletionSource promise, IInternalLogger logger)
        {
            if (promise != TaskCompletionSource.Void && !promise.TryComplete())
            {
                logger.Warn($"Failed to mark a promise as success because it is done already: {promise}");
            }
        }

        /// <summary>
        ///     Marks the specified {@code promise} as failure.  If the {@code promise} is done already, log a message.
        /// </summary>
        public static void SafeSetFailure(TaskCompletionSource promise, Exception cause, IInternalLogger logger)
        {
            if (promise != TaskCompletionSource.Void && !promise.TrySetException(cause))
            {
                logger.Warn($"Failed to mark a promise as failure because it's done already: {promise}", cause);
            }
        }

        public static void CloseSafe(this IChannel channel)
        {
            CompleteChannelCloseTaskSafely(channel, channel.CloseAsync());
        }

        public static void CloseSafe(this IChannelUnsafe u)
        {
            CompleteChannelCloseTaskSafely(u, u.CloseAsync());
        }

        internal static async void CompleteChannelCloseTaskSafely(object channelObject, Task closeTask)
        {
            try
            {
                await closeTask;
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex) 
            {
                if (Log.DebugEnabled)
                {
                    Log.Debug("Failed to close channel " + channelObject + " cleanly.", ex);
                }
            }
        }
    }
}