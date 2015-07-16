// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// Extension methods used for slicing byte arrays
    /// </summary>
    public static class ByteArrayExtensions
    {
        public static readonly byte[] Empty = new byte[0];

        public static byte[] Slice(this byte[] array, int length)
        {
            Contract.Requires(array != null);

            if (length > array.Length)
            {
                throw new ArgumentOutOfRangeException("length", string.Format("length({0}) cannot be longer than Array.length({1})", length, array.Length));
            }
            return Slice(array, 0, length);
        }

        public static byte[] Slice(this byte[] array, int index, int length)
        {
            Contract.Requires(array != null);

            if (index + length > array.Length)
            {
                throw new ArgumentOutOfRangeException("length", string.Format("index: ({0}), length({1}) index + length cannot be longer than Array.length({2})", index, length, array.Length));
            }
            var result = new byte[length];
            Array.Copy(array, index, result, 0, length);
            return result;
        }

        public static void SetRange(this byte[] array, int index, byte[] src)
        {
            SetRange(array, index, src, 0, src.Length);
        }

        public static void SetRange(this byte[] array, int index, byte[] src, int srcIndex, int srcLength)
        {
            Contract.Requires(array != null);
            Contract.Requires(src != null);
            if (index + srcLength > array.Length)
            {
                throw new ArgumentOutOfRangeException("srcLength", string.Format("index: ({0}), srcLength({1}) index + length cannot be longer than Array.length({2})", index, srcLength, array.Length));
            }
            if (srcIndex + srcLength > src.Length)
            {
                throw new ArgumentOutOfRangeException("srcLength", string.Format("index: ({0}), srcLength({1}) index + length cannot be longer than src.length({2})", srcIndex, srcLength, src.Length));
            }

            Array.Copy(src, srcIndex, array, index, srcLength);
        }
    }
}