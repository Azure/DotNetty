// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests
{
    using System.IO;
    using DotNetty.Buffers;
    using DotNetty.Handlers.Streams;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpChunkedInputTest
    {
        static readonly byte[] Bytes = new byte[1024 * 64];

        static HttpChunkedInputTest()
        {
            for (int i = 0; i < Bytes.Length; i++)
            {
                Bytes[i] = (byte)i;
            }
        }

        [Fact]
        public void ChunkedStream()
        {
            var stream = new ChunkedStream(new MemoryStream(Bytes));
            Check(new HttpChunkedInput(stream));
        }

        [Fact]
        public void WrappedReturnNull()
        {
            var input = new EmptyChunkedInput();
            var httpInput = new HttpChunkedInput(input);

            IHttpContent result = httpInput.ReadChunk(PooledByteBufferAllocator.Default);
            Assert.Null(result);
        }

        sealed class EmptyChunkedInput : IChunkedInput<IByteBuffer>
        {
            public bool IsEndOfInput => false;

            public void Close()
            {
                // NOOP
            }

            public IByteBuffer ReadChunk(IByteBufferAllocator allocator) => null;

            public long Length => 0;

            public long Progress => 0;
        }

        static void Check(params IChunkedInput<IHttpContent>[] inputs)
        {
            var ch = new EmbeddedChannel(new ChunkedWriteHandler<IHttpContent>());

            foreach (IChunkedInput<IHttpContent> input in inputs)
            {
                ch.WriteOutbound(input);
            }
            Assert.True(ch.Finish());

            int i = 0;
            int read = 0;
            IHttpContent lastHttpContent = null;
            for (;;)
            {
                var httpContent = ch.ReadOutbound<IHttpContent>();
                if (httpContent == null)
                {
                    break;
                }

                if (lastHttpContent != null)
                {
                    Assert.True(lastHttpContent is DefaultHttpContent, "Chunk must be DefaultHttpContent");
                }

                IByteBuffer buffer = httpContent.Content;
                while (buffer.IsReadable())
                {
                    Assert.Equal(Bytes[i++], buffer.ReadByte());
                    read++;
                    if (i == Bytes.Length)
                    {
                        i = 0;
                    }
                }
                buffer.Release();

                // Save last chunk
                lastHttpContent = httpContent;
            }

            Assert.Equal(Bytes.Length * inputs.Length, read);

            //Last chunk must be EmptyLastHttpContent.Default
            Assert.Same(EmptyLastHttpContent.Default, lastHttpContent);
        }
    }
}
