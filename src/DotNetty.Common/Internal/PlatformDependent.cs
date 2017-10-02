// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public static class PlatformDependent
    {
        static int seed = (int)(Stopwatch.GetTimestamp() & 0xFFFFFFFF); //used to safly cast long to int, because the timestamp returned is long and it doesn't fit into an int
        static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed))); //used to simulate java ThreadLocalRandom

        public static IQueue<T> NewFixedMpscQueue<T>(int capacity) where T : class => new MpscArrayQueue<T>(capacity);

        public static IQueue<T> NewMpscQueue<T>() where T : class => new CompatibleConcurrentQueue<T>();

        public static ILinkedQueue<T> SpscLinkedQueue<T>() where T : class => new SpscLinkedQueue<T>();

        public static Random GetThreadLocalRandom() => ThreadLocalRandom.Value;

        public static unsafe void CopyMemory(byte[] src, int srcIndex, byte[] dst, int dstIndex, int length)
        {
            if (length == 0)
            {
                return;
            }

            fixed (byte* source = &src[srcIndex])
                fixed (byte* destination = &dst[dstIndex])
                    Unsafe.CopyBlock(destination, source, (uint)length);
        }

        public static unsafe void Clear(byte[] src, int srcIndex, int length)
        {
            if (length == 0)
            {
                return;
            }

            fixed (void* source = &src[srcIndex])
                Unsafe.InitBlock(source, default(byte), (uint)length);
        }
    }
}