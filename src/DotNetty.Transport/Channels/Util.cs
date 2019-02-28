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
        /// Marks the specified <see cref="TaskCompletionSource"/> as success. If the
        /// <see cref="TaskCompletionSource"/> is done already, logs a message.
        /// </summary>
        /// <param name="promise">The <see cref="TaskCompletionSource"/> to complete.</param>
        /// <param name="logger">The <see cref="IInternalLogger"/> to use to log a failure message.</param>
        public static void SafeSetSuccess(TaskCompletionSource promise, IInternalLogger logger)
        {
            if (promise != TaskCompletionSource.Void && !promise.TryComplete())
            {
                logger.Warn($"Failed to mark a promise as success because it is done already: {promise}");
            }
        }

        /// <summary>
        /// Marks the specified <see cref="TaskCompletionSource"/> as failure. If the
        /// <see cref="TaskCompletionSource"/> is done already, log a message.
        /// </summary>
        /// <param name="promise">The <see cref="TaskCompletionSource"/> to complete.</param>
        /// <param name="cause">The <see cref="Exception"/> to fail the <see cref="TaskCompletionSource"/> with.</param>
        /// <param name="logger">The <see cref="IInternalLogger"/> to use to log a failure message.</param>
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