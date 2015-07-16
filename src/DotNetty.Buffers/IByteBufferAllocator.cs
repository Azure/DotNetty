// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    /// <summary>
    /// Thread-safe interface for allocating <see cref="IByteBuffer"/> instances for use inside Helios reactive I/O
    /// </summary>
    public interface IByteBufferAllocator
    {
        IByteBuffer Buffer();

        IByteBuffer Buffer(int initialCapacity);

        IByteBuffer Buffer(int initialCapacity, int maxCapacity);

        CompositeByteBuffer CompositeBuffer();

        CompositeByteBuffer CompositeBuffer(int maxComponents);
    }
}