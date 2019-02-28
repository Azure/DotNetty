// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Streams
{
    using DotNetty.Buffers;

    public interface IChunkedInput<out T>
    {
        bool IsEndOfInput { get; }

        void Close();

        T ReadChunk(IByteBufferAllocator allocator);

        long Length { get; }

        long Progress { get; }
    }
}
