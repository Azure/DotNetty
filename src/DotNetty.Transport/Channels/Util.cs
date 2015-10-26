// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;

    static class Util
    {
        /// <summary>
        /// Marks the specified {@code promise} as success.  If the {@code promise} is done already, log a message.
        /// </summary>
        public static void SafeSetSuccess(TaskCompletionSource promise, IInternalLogger logger)
        {
            if (promise != TaskCompletionSource.Void && !promise.TryComplete())
            {
                logger.Warn(string.Format("Failed to mark a promise as success because it is done already: {0}", promise));
            }
        }

        /// <summary>
        /// Marks the specified {@code promise} as failure.  If the {@code promise} is done already, log a message.
        /// </summary>
        public static void SafeSetFailure(TaskCompletionSource promise, Exception cause, IInternalLogger logger)
        {
            if (promise != TaskCompletionSource.Void && !promise.TrySetException(cause))
            {
                logger.Warn(string.Format("Failed to mark a promise as failure because it's done already: {0}", promise), cause);
            }
        }
    }
}