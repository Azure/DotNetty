// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.Multipart;
    using Xunit;

    public sealed class AbstractMemoryHttpDataTest
    {
        [Fact]
        public void SetContentFromStream()
        {
            var random = new Random();

            for (int i = 0; i < 20; i++)
            {
                // Generate input data bytes.
                int size = random.Next(short.MaxValue);
                var bytes = new byte[size];

                random.NextBytes(bytes);

                // Generate parsed HTTP data block.
                var httpData = new TestHttpData("name", Encoding.UTF8, 0);

                httpData.SetContent(new MemoryStream(bytes));

                // Validate stored data.
                IByteBuffer buffer = httpData.GetByteBuffer();

                Assert.Equal(0, buffer.ReaderIndex);
                Assert.Equal(bytes.Length, buffer.WriterIndex);

                var data = new byte[bytes.Length];
                buffer.GetBytes(buffer.ReaderIndex, data);

                Assert.True(data.SequenceEqual(bytes));
            }
        }

        sealed class TestHttpData : AbstractMemoryHttpData
        {
            public TestHttpData(string name, Encoding contentEncoding, long size)
                : base(name, contentEncoding, size)
            {
            }

            public override int CompareTo(IInterfaceHttpData other)
            {
                throw new NotSupportedException("Should never be called.");
            }

            public override HttpDataType DataType => throw new NotSupportedException("Should never be called.");

            public override IByteBufferHolder Copy()
            {
                throw new NotSupportedException("Should never be called.");
            }

            public override IByteBufferHolder Duplicate()
            {
                throw new NotSupportedException("Should never be called.");
            }

            public override IByteBufferHolder RetainedDuplicate()
            {
                throw new NotSupportedException("Should never be called.");
            }

            public override IByteBufferHolder Replace(IByteBuffer content)
            {
                throw new NotSupportedException("Should never be called.");
            }
        }
    }
}
