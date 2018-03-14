// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Runtime.CompilerServices;

    static class ThrowHelper
    {
        public static void ThrowIndexOutOfRangeException(string message) => throw new IndexOutOfRangeException(message);

        public static void ThrowIndexOutOfRangeException(string format, int index, int length, int capacity) => throw CreateIndexOutOfRangeException(format, index, length, capacity);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Exception CreateIndexOutOfRangeException(string format, int index, int length, int capacity) => new IndexOutOfRangeException(string.Format(format, index, length, capacity));

        public static void ThrowIllegalReferenceCountException(int count = 0) => throw CreateIllegalReferenceCountException(count);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Exception CreateIllegalReferenceCountException(int count) => throw new IllegalReferenceCountException(count);

        public static void ThrowArgumentNullException(string name) => throw new ArgumentNullException(name);

        public static void ThrowArgumentOutOfRangeException(string name, string message) => throw new ArgumentOutOfRangeException(name, message);

        public static void ThrowArgumentOutOfRangeException_LessThanZero(string name, int value)
        {
            throw CreateArgumentOutOfRangeException_LessThanZero(name, value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Exception CreateArgumentOutOfRangeException_LessThanZero(string name, int value)
        {
            return new ArgumentOutOfRangeException(name, $"{name}: {value} (expected: >= 0)");
        }
    }
}
