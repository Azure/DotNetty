// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Threading;
    using DotNetty.Common.Internal.Logging;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public static class ReferenceCountUtil
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(ReferenceCountUtil));

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Retain()"/> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing.
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
        /// Tries to call <see cref="IReferenceCounted.Retain(int)"/> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing.
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
        /// Tries to call <see cref="IReferenceCounted.Touch()" /> if the specified message implements
        /// <see cref="IReferenceCounted" />.
        /// If the specified message doesn't implement <see cref="IReferenceCounted" />, this method does nothing.
        /// </summary>
        public static T Touch<T>(T msg)
        {
            var refCnt = msg as IReferenceCounted;
            if (refCnt != null)
            {
                return (T)refCnt.Touch();
            }
            return msg;
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Touch(object)" /> if the specified message implements
        /// <see cref="IReferenceCounted" />. If the specified message doesn't implement
        /// <see cref="IReferenceCounted" />, this method does nothing.
        /// </summary>
        public static T Touch<T>(T msg, object hint)
        {
            var refCnt = msg as IReferenceCounted;
            if (refCnt != null)
            {
                return (T)refCnt.Touch(hint);
            }
            return msg;
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Release()" /> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing.
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
        /// Tries to call <see cref="IReferenceCounted.Release(int)" /> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing.
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
        /// Tries to call <see cref="IReferenceCounted.Release()" /> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing. Unlike <see cref="Release(object)"/>, this
        /// method catches an exception raised by <see cref="IReferenceCounted.Release()" /> and logs it, rather than
        /// rethrowing it to the caller. It is usually recommended to use <see cref="Release(object)"/> instead, unless
        /// you absolutely need to swallow an exception.
        /// </summary>
        public static void SafeRelease(object msg)
        {
            try
            {
                Release(msg);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to release a message: {}", msg, ex);
            }
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Release(int)" /> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing. Unlike <see cref="Release(object)"/>, this
        /// method catches an exception raised by <see cref="IReferenceCounted.Release(int)" /> and logs it, rather
        /// than rethrowing it to the caller. It is usually recommended to use <see cref="Release(object, int)"/>
        /// instead, unless you absolutely need to swallow an exception.
        /// </summary>
        public static void SafeRelease(object msg, int decrement)
        {
            try
            {
                Release(msg, decrement);
            }
            catch (Exception ex)
            {
                if (Logger.WarnEnabled)
                {
                    Logger.Warn("Failed to release a message: {} (decrement: {})", msg, decrement, ex);
                }
            }
        }

        public static void SafeRelease(this IReferenceCounted msg)
        {
            try
            {
                msg?.Release();
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to release a message: {}", msg, ex);
            }
        }

        public static void SafeRelease(this IReferenceCounted msg, int decrement)
        {
            try
            {
                msg?.Release(decrement);
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to release a message: {} (decrement: {})", msg, decrement, ex);
            }
        }

        /// <summary>
        /// Schedules the specified object to be released when the caller thread terminates. Note that this operation
        /// is intended to simplify reference counting of ephemeral objects during unit tests. Do not use it beyond the
        /// intended use case.
        /// </summary>
        public static T ReleaseLater<T>(T msg) => ReleaseLater(msg, 1);

        /// <summary>
        /// Schedules the specified object to be released when the caller thread terminates. Note that this operation
        /// is intended to simplify reference counting of ephemeral objects during unit tests. Do not use it beyond the
        /// intended use case.
        /// </summary>
        public static T ReleaseLater<T>(T msg, int decrement)
        {
            var referenceCounted = msg as IReferenceCounted;
            if (referenceCounted != null)
            {
                ThreadDeathWatcher.Watch(Thread.CurrentThread, () =>
                {
                    try
                    {
                        if (!referenceCounted.Release(decrement))
                        {
                            Logger.Warn("Non-zero refCnt: {}", FormatReleaseString(referenceCounted, decrement));
                        }
                        else
                        {
                            Logger.Debug("Released: {}", FormatReleaseString(referenceCounted, decrement));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Failed to release an object: {}", referenceCounted, ex);
                    }
                });
            }
            return msg;
        }

        static string FormatReleaseString(IReferenceCounted referenceCounted, int decrement)
            => $"{referenceCounted.GetType().Name}.Release({decrement.ToString()}) refCnt: {referenceCounted.ReferenceCount.ToString()}";
    }
}