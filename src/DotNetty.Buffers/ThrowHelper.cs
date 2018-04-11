// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable UseStringInterpolation
// ReSharper disable UseNameofExpression
// ReSharper disable NotResolvedInText
namespace DotNetty.Buffers
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_Index(int index, int length, int capacity)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("index: {0}, length: {1} (expected: range(0, {2}))", index, length, capacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_SrcIndex(int srcIndex)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("srcIndex: {0}", srcIndex));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_SrcIndex(int srcIndex, int length, int srcCapacity)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("srcIndex: {0}, length: {1} (expected: range(0, {2}))", srcIndex, length, srcCapacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_DstIndex(int dstIndex)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("dstIndex: {0}", dstIndex));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_DstIndex(int dstIndex, int length,  int dstCapacity)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("dstIndex: {0}, length: {1} (expected: range(0, {2}))", dstIndex, length, dstCapacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_ReaderIndex(int index, int writerIndex)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("ReaderIndex: {0} (expected: 0 <= readerIndex <= writerIndex({1})", index, writerIndex));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_ReaderIndex(int minimumReadableBytes, int readerIndex, int writerIndex, AbstractByteBuffer buf)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("readerIndex({0}) + length({1}) exceeds writerIndex({2}): {3}", readerIndex, minimumReadableBytes, writerIndex, buf));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_WriterIndex(int index, int readerIndex, int capacity)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("WriterIndex: {0} (expected: 0 <= readerIndex({1}) <= writerIndex <= capacity ({2})", index, readerIndex, capacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_WriterIndex(int minWritableBytes, int writerIndex, int maxCapacity, AbstractByteBuffer buf)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format($"writerIndex({0}) + minWritableBytes({1}) exceeds maxCapacity({2}): {3}", writerIndex, minWritableBytes, maxCapacity, buf));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_ReaderWriterIndex(int readerIndex, int writerIndex, int capacity)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("ReaderIndex: {0}, WriterIndex: {1} (expected: 0 <= readerIndex <= writerIndex <= capacity ({2})", readerIndex, writerIndex, capacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_ReadableBytes(int length, IByteBuffer src)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("length({0}) exceeds src.readableBytes({1}) where src is: {2}", length, src.ReadableBytes, src));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_WritableBytes(int length, IByteBuffer dst)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("length({0}) exceeds destination.WritableBytes({1}) where destination is: {2}", length, dst.WritableBytes, dst));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_Src(int srcIndex, int length, int count)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= srcIdx({0}) <= srcIdx + length({1}) <= srcLen({2})", srcIndex, length, count));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIllegalReferenceCountException(int count)
        {
            throw GetIllegalReferenceCountException();

            IllegalReferenceCountException GetIllegalReferenceCountException()
            {
                return new IllegalReferenceCountException(count);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_MinimumReadableBytes(int minimumReadableBytes)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("minimumReadableBytes", string.Format("minimumReadableBytes: {0} (expected: >= 0)", minimumReadableBytes));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_InitialCapacity()
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("initialCapacity", "initialCapacity must be greater than zero");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_InitialCapacity(int initialCapacity, int maxCapacity)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("initialCapacity", string.Format("initialCapacity ({0}) must be greater than maxCapacity ({1})", initialCapacity, maxCapacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_MinWritableBytes()
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("minWritableBytes", "expected minWritableBytes to be greater than zero");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_MinNewCapacity(int minNewCapacity)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("minNewCapacity", string.Format("minNewCapacity: {0} (expected: 0+)", minNewCapacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_MaxCapacity(int minNewCapacity, int maxCapacity)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("maxCapacity", string.Format("minNewCapacity: {0} (expected: not greater than maxCapacity({1})", minNewCapacity, maxCapacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Capacity(int newCapacity, int maxCapacity)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("newCapacity", string.Format($"newCapacity: {0} (expected: 0-{1})", newCapacity, maxCapacity));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNullException_Dst()
        {
            throw GetArgumentOutOfRangeException();

            ArgumentNullException GetArgumentOutOfRangeException()
            {
                return new ArgumentNullException("dst");
            }
        }
    }
}
