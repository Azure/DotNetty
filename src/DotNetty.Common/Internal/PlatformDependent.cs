// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using DotNetty.Common.Utilities;

    public static class PlatformDependent
    {
        public static IQueue<T> NewFixedMpscQueue<T>(int capacity)
            where T : class
        {
            return new MpscArrayQueue<T>(capacity);
        }

        public static IQueue<T> NewMpscQueue<T>()
            where T : class
        {
            return new MpscLinkedQueue<T>();
        }
    }
}