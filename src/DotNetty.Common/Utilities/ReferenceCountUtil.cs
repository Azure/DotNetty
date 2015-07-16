// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    public sealed class ReferenceCountUtil
    {
        //private static readonly InternalLogger logger = InternalLoggerFactory.getInstance(ReferenceCountUtil.class);

        /// <summary>
        /// Try to call {@link ReferenceCounted#retain()} if the specified message implements {@link ReferenceCounted}.
        /// If the specified message doesn't implement {@link ReferenceCounted}, this method does nothing.
        /// </summary>
        public static T Retain<T>(T msg)
        {
            var counted = msg as IReferenceCounted;
            if (counted != null)
            {
                return (T)counted.Retain();
            }
            return msg;
        }

        /// <summary>
        /// Try to call {@link ReferenceCounted#retain(int)} if the specified message implements {@link ReferenceCounted}.
        /// If the specified message doesn't implement {@link ReferenceCounted}, this method does nothing.
        /// </summary>
        public static T Retain<T>(T msg, int increment)
        {
            var counted = msg as IReferenceCounted;
            if (counted != null)
            {
                return (T)counted.Retain(increment);
            }
            return msg;
        }

        /// <summary>
        /// Try to call {@link ReferenceCounted#release()} if the specified message implements {@link ReferenceCounted}.
        /// If the specified message doesn't implement {@link ReferenceCounted}, this method does nothing.
        /// </summary>
        public static bool Release(object msg)
        {
            var counted = msg as IReferenceCounted;
            if (counted != null)
            {
                return counted.Release();
            }
            return false;
        }

        /// <summary>
        /// Try to call {@link ReferenceCounted#release(int)} if the specified message implements {@link ReferenceCounted}.
        /// If the specified message doesn't implement {@link ReferenceCounted}, this method does nothing.
        /// </summary>
        public static bool Release(object msg, int decrement)
        {
            var counted = msg as IReferenceCounted;
            if (counted != null)
            {
                return counted.Release(decrement);
            }
            return false;
        }

        /// <summary>
        /// Try to call {@link ReferenceCounted#release()} if the specified message implements {@link ReferenceCounted}.
        /// If the specified message doesn't implement {@link ReferenceCounted}, this method does nothing.
        /// Unlike {@link #release(Object)} this method catches an exception raised by {@link ReferenceCounted#release()}
        /// and logs it, rather than rethrowing it to the caller.  It is usually recommended to use {@link #release(Object)}
        /// instead, unless you absolutely need to swallow an exception.
        /// </summary>
        public static void SafeRelease(object msg)
        {
            try
            {
                Release(msg);
            }
            catch (Exception ex)
            {
                // todo: log
                //logger.warn("Failed to release a message: {}", msg, t);
            }
        }

        /// <summary>
        /// Try to call {@link ReferenceCounted#release(int)} if the specified message implements {@link ReferenceCounted}.
        /// If the specified message doesn't implement {@link ReferenceCounted}, this method does nothing.
        /// Unlike {@link #release(Object)} this method catches an exception raised by {@link ReferenceCounted#release(int)}
        /// and logs it, rather than rethrowing it to the caller.  It is usually recommended to use
        /// {@link #release(Object, int)} instead, unless you absolutely need to swallow an exception.
        /// </summary>
        public static void SafeRelease(object msg, int decrement)
        {
            try
            {
                Release(msg, decrement);
            }
            catch (Exception ex)
            {
                // todo: log
                //if (logger.isWarnEnabled()) {
                //    logger.warn("Failed to release a message: {} (decrement: {})", msg, decrement, t);
                //}
            }
        }
    }
}