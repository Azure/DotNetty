// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    /// <summary>
    ///     Thread-safe interface for allocating <see cref="IByteBuffer" />/.
    /// </summary>
    public interface IByteBufferAllocator
    {
        IByteBuffer Buffer();

        IByteBuffer Buffer(int initialCapacity);

        IByteBuffer Buffer(int initialCapacity, int maxCapacity);

        IByteBuffer HeapBuffer();

        IByteBuffer HeapBuffer(int initialCapacity);

        IByteBuffer HeapBuffer(int initialCapacity, int maxCapacity);

        IByteBuffer DirectBuffer();

        IByteBuffer DirectBuffer(int initialCapacity);

        IByteBuffer DirectBuffer(int initialCapacity, int maxCapacity);

        CompositeByteBuffer CompositeBuffer();

        CompositeByteBuffer CompositeBuffer(int maxComponents);

        CompositeByteBuffer CompositeHeapBuffer();

        CompositeByteBuffer CompositeHeapBuffer(int maxComponents);

        CompositeByteBuffer CompositeDirectBuffer();

        CompositeByteBuffer CompositeDirectBuffer(int maxComponents);

        bool IsDirectBufferPooled { get; }

        int CalculateNewCapacity(int minNewCapacity, int maxCapacity);
    }
}