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
    using DotNetty.Common.Utilities;

    using static PlatformDependent0;

    public static class PlatformDependent
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(PlatformDependent));

        static readonly bool UseDirectBuffer;

        static PlatformDependent()
        {
            UseDirectBuffer = !SystemPropertyUtil.GetBoolean("io.netty.noPreferDirect", true);
            if (Logger.DebugEnabled)
            {
                Logger.Debug("-Dio.netty.noPreferDirect: {}", !UseDirectBuffer);
            }
        }

        public static bool DirectBufferPreferred => UseDirectBuffer;

        static int seed = (int)(Stopwatch.GetTimestamp() & 0xFFFFFFFF); //used to safly cast long to int, because the timestamp returned is long and it doesn't fit into an int
        static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed))); //used to simulate java ThreadLocalRandom
        static readonly bool IsLittleEndian = BitConverter.IsLittleEndian;

        public static IQueue<T> NewFixedMpscQueue<T>(int capacity) where T : class => new MpscArrayQueue<T>(capacity);

        public static IQueue<T> NewMpscQueue<T>() where T : class => new CompatibleConcurrentQueue<T>();

        public static IDictionary<TKey, TValue> NewConcurrentHashMap<TKey, TValue>() => new ConcurrentDictionary<TKey, TValue>();

        public static ILinkedQueue<T> NewSpscLinkedQueue<T>() where T : class => new SpscLinkedQueue<T>();

        public static Random GetThreadLocalRandom() => ThreadLocalRandom.Value;

        public static unsafe bool ByteArrayEquals(byte[] bytes1, int startPos1, byte[] bytes2, int startPos2, int length)
        {
            if (length <= 0)
            {
                return true;
            }

            fixed (byte* array1 = &bytes1[startPos1])
                fixed (byte* array2 = &bytes2[startPos2])
                    return PlatformDependent0.ByteArrayEquals(array1, array2, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int HashCodeAscii(byte[] bytes, int startPos, int length)
        {
            if (length == 0)
            {
                return HashCodeAsciiSeed;
            }

            fixed (byte* array = &bytes[startPos])
                return PlatformDependent0.HashCodeAscii(array, length);
        }

        public static int HashCodeAscii(ICharSequence bytes)
        {
            int hash = HashCodeAsciiSeed;
            int remainingBytes = bytes.Count & 7;

            // Benchmarking shows that by just naively looping for inputs 8~31 bytes long we incur a relatively large
            // performance penalty (only achieve about 60% performance of loop which iterates over each char). So because
            // of this we take special provisions to unroll the looping for these conditions.
            switch (bytes.Count)
            {
                case 31:
                case 30:
                case 29:
                case 28:
                case 27:
                case 26:
                case 25:
                case 24:
                    hash = HashCodeAsciiCompute(
                        bytes,
                        bytes.Count - 24,
                        HashCodeAsciiCompute(
                            bytes,
                            bytes.Count - 16,
                            HashCodeAsciiCompute(bytes, bytes.Count - 8, hash)));
                    break;
                case 23:
                case 22:
                case 21:
                case 20:
                case 19:
                case 18:
                case 17:
                case 16:
                    hash = HashCodeAsciiCompute(
                        bytes,
                        bytes.Count - 16,
                        HashCodeAsciiCompute(bytes, bytes.Count - 8, hash));
                    break;
                case 15:
                case 14:
                case 13:
                case 12:
                case 11:
                case 10:
                case 9:
                case 8:
                    hash = HashCodeAsciiCompute(bytes, bytes.Count - 8, hash);
                    break;
                case 7:
                case 6:
                case 5:
                case 4:
                case 3:
                case 2:
                case 1:
                case 0:
                    break;
                default:
                    for (int i = bytes.Count - 8; i >= remainingBytes; i -= 8)
                    {
                        hash = HashCodeAsciiCompute(bytes, i, hash);
                    }
                    break;
            }
            switch (remainingBytes)
            {
                case 7:
                    return ((hash
                        * HashCodeC1 + HashCodeAsciiSanitizsByte(bytes[0]))
                        * HashCodeC2 + HashCodeAsciiSanitizeShort(bytes, 1))
                        * HashCodeC1 + HashCodeAsciiSanitizeInt(bytes, 3);
                case 6:
                    return (hash
                        * HashCodeC1 + HashCodeAsciiSanitizeShort(bytes, 0))
                        * HashCodeC2 + HashCodeAsciiSanitizeInt(bytes, 2);
                case 5:
                    return (hash
                        * HashCodeC1 + HashCodeAsciiSanitizsByte(bytes[0]))
                        * HashCodeC2 + HashCodeAsciiSanitizeInt(bytes, 1);
                case 4:
                    return hash
                        * HashCodeC1 + HashCodeAsciiSanitizeInt(bytes, 0);
                case 3:
                    return (hash
                        * HashCodeC1 + HashCodeAsciiSanitizsByte(bytes[0]))
                        * HashCodeC2 + HashCodeAsciiSanitizeShort(bytes, 1);
                case 2:
                    return hash
                        * HashCodeC1 + HashCodeAsciiSanitizeShort(bytes, 0);
                case 1:
                    return hash
                        * HashCodeC1 + HashCodeAsciiSanitizsByte(bytes[0]);
                default:
                    return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int HashCodeAsciiCompute(ICharSequence value, int offset, int hash)
        {
            if (!IsLittleEndian)
            {
                return hash * HashCodeC1 +
                    // Low order int
                    HashCodeAsciiSanitizeInt(value, offset + 4) * HashCodeC2 +
                    // High order int
                    HashCodeAsciiSanitizeInt(value, offset);
            }
            return hash * HashCodeC1 +
                // Low order int
                HashCodeAsciiSanitizeInt(value, offset) * HashCodeC2 +
                // High order int
                HashCodeAsciiSanitizeInt(value, offset + 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int HashCodeAsciiSanitizeInt(ICharSequence value, int offset)
        {
            if (!IsLittleEndian)
            {
                // mimic a unsafe.getInt call on a big endian machine
                return (value[offset + 3] & 0x1f)
                    | (value[offset + 2] & 0x1f) << 8
                    | (value[offset + 1] & 0x1f) << 16
                    | (value[offset] & 0x1f) << 24;
            }

            return (value[offset + 3] & 0x1f) << 24
                | (value[offset + 2] & 0x1f) << 16
                | (value[offset + 1] & 0x1f) << 8
                | (value[offset] & 0x1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int HashCodeAsciiSanitizeShort(ICharSequence value, int offset)
        {
            if (!IsLittleEndian)
            {
                // mimic a unsafe.getShort call on a big endian machine
                return (value[offset + 1] & 0x1f)
                    | (value[offset] & 0x1f) << 8;
            }

            return (value[offset + 1] & 0x1f) << 8
                | (value[offset] & 0x1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int HashCodeAsciiSanitizsByte(char value) => value & 0x1f;

        public static void CopyMemory(byte[] src, int srcIndex, byte[] dst, int dstIndex, int length)
        {
            if (length > 0)
            {
                Unsafe.CopyBlockUnaligned(ref dst[dstIndex], ref src[srcIndex], unchecked((uint)length));
            }
        }

        public static unsafe void CopyMemory(byte* src, byte* dst, int length)
        {
            if (length > 0)
            {
                Unsafe.CopyBlockUnaligned(dst, src, unchecked((uint)length));
            }
        }

        public static unsafe void CopyMemory(byte* src, byte[] dst, int dstIndex, int length)
        {
            if (length > 0)
            {
                fixed (byte* destination = &dst[dstIndex])
                    Unsafe.CopyBlockUnaligned(destination, src, unchecked((uint)length));
            }
        }

        public static unsafe void CopyMemory(byte[] src, int srcIndex, byte* dst, int length)
        {
            if (length > 0)
            {
                fixed (byte* source = &src[srcIndex])
                    Unsafe.CopyBlockUnaligned(dst, source, unchecked((uint)length));
            }
        }

        public static void Clear(byte[] src, int srcIndex, int length)
        {
            if (length > 0)
            {
                Unsafe.InitBlockUnaligned(ref src[srcIndex], default(byte), unchecked((uint)length));
            }
        }

        public static unsafe void SetMemory(byte* src, int length, byte value)
        {
            if (length > 0)
            {
                Unsafe.InitBlockUnaligned(src, value, unchecked((uint)length));
            }
        }

        public static void SetMemory(byte[] src, int srcIndex, int length, byte value)
        {
            if (length > 0)
            {
                Unsafe.InitBlockUnaligned(ref src[srcIndex], value, unchecked((uint)length));
            }
        }
    }
}