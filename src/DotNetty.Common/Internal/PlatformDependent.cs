// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Common.Internal
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using DotNetty.Common.Internal.Logging;

    public static class PlatformDependent
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(PlatformDependent));

        static readonly bool UseDirectBuffer;

        static PlatformDependent()
        {
            UseDirectBuffer = !SystemPropertyUtil.GetBoolean("io.netty.noPreferDirect", false);
            if (Logger.DebugEnabled)
            {
                Logger.Debug("-Dio.netty.noPreferDirect: {}", !UseDirectBuffer);
            }
        }

        public static bool DirectBufferPreferred => UseDirectBuffer;

        static int seed = (int)(Stopwatch.GetTimestamp() & 0xFFFFFFFF); //used to safly cast long to int, because the timestamp returned is long and it doesn't fit into an int
        static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed))); //used to simulate java ThreadLocalRandom

        public static IQueue<T> NewFixedMpscQueue<T>(int capacity) where T : class => new MpscArrayQueue<T>(capacity);

        public static IQueue<T> NewMpscQueue<T>() where T : class => new CompatibleConcurrentQueue<T>();

        public static IDictionary<TKey, TValue> NewConcurrentHashMap<TKey, TValue>() => new ConcurrentDictionary<TKey, TValue>();

        public static ILinkedQueue<T> NewSpscLinkedQueue<T>() where T : class => new SpscLinkedQueue<T>();

        public static Random GetThreadLocalRandom() => ThreadLocalRandom.Value;

        public static unsafe bool ByteArrayEquals(byte[] bytes1, int startPos1, byte[] bytes2, int startPos2, int length)
        {
            fixed (byte* array1 = bytes1)
            fixed (byte* array2 = bytes2)
                return PlatformDependent0.ByteArrayEquals(array1, startPos1, array2, startPos2, length);
        }

        public static unsafe void CopyMemory(byte[] src, int srcIndex, byte[] dst, int dstIndex, int length)
        {
            if (length > 0)
            {
                fixed (byte* source = &src[srcIndex])
                    fixed (byte* destination = &dst[dstIndex])
                        Unsafe.CopyBlock(destination, source, unchecked((uint)length));
            }
        }

        public static unsafe void CopyMemory(byte* src, byte* dst, int length)
        {
            if (length > 0)
            {
                Unsafe.CopyBlock(dst, src, unchecked((uint)length));
            }
        }

        public static unsafe void CopyMemory(byte* src, byte[] dst, int dstIndex, int length)
        {
            if (length > 0)
            {
                fixed (byte* destination = &dst[dstIndex])
                    Unsafe.CopyBlock(destination, src, unchecked((uint)length));
            }
        }

        public static unsafe void CopyMemory(byte[] src, int srcIndex, byte* dst, int length)
        {
            if (length > 0)
            {
                fixed (byte* source = &src[srcIndex])
                    Unsafe.CopyBlock(dst, source, unchecked((uint)length));
            }
        }

        public static unsafe void Clear(byte[] src, int srcIndex, int length)
        {
            if (length > 0)
            {
                fixed (void* source = &src[srcIndex])
                    Unsafe.InitBlock(source, default(byte), unchecked((uint)length));
            }
        }

        public static unsafe void SetMemory(byte* src, int length, byte value)
        {
            if (length > 0)
            {
                Unsafe.InitBlock(src, value, unchecked((uint)length));
            }
        }

        public static unsafe void SetMemory(byte[] src, int srcIndex, int length, byte value)
        {
            if (length > 0)
            {
                fixed (byte* source = &src[srcIndex])
                    Unsafe.InitBlock(source, value, unchecked((uint)length));
            }
        }
    }
}