// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using DotNetty.Buffers;
    using DotNetty.Handlers.Streams;

    public class HttpChunkedInput : IChunkedInput<IHttpContent>
    {
        readonly IChunkedInput<IByteBuffer> input;
        readonly ILastHttpContent lastHttpContent;
        bool sentLastChunk;

        public HttpChunkedInput(IChunkedInput<IByteBuffer> input)
        {
            this.input = input;
            this.lastHttpContent = EmptyLastHttpContent.Default;
        }

        public HttpChunkedInput(IChunkedInput<IByteBuffer> input, ILastHttpContent lastHttpContent)
        {
            this.input = input;
            this.lastHttpContent = lastHttpContent;
        }

        public bool IsEndOfInput => this.input.IsEndOfInput && this.sentLastChunk;

        public void Close() => this.input.Close();

        public IHttpContent ReadChunk(IByteBufferAllocator allocator)
        {
            if (this.input.IsEndOfInput)
            {
                if (this.sentLastChunk)
                {
                    return null;
                }
                // Send last chunk for this input
                this.sentLastChunk = true;
                return this.lastHttpContent;
            }
            else
            {
                IByteBuffer buf = this.input.ReadChunk(allocator);
                return buf == null ? null : new DefaultHttpContent(buf);
            }
        }

        public long Length => this.input.Length;

        public long Progress => this.input.Progress;
    }
}
