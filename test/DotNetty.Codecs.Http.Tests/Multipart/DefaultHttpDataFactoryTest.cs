// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.Multipart;
    using Xunit;

    public sealed class DefaultHttpDataFactoryTest : IDisposable
    {
        // req1 equals req2
        readonly IHttpRequest req1 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/form");
        readonly IHttpRequest req2 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/form");
        readonly DefaultHttpDataFactory factory;

        public DefaultHttpDataFactoryTest()
        {
            // Before doing anything, assert that the requests are equal
            Assert.Equal(this.req1.GetHashCode(), this.req2.GetHashCode());
            Assert.True(this.req1.Equals(this.req2));

            this.factory = new DefaultHttpDataFactory();
        }

        [Fact]
        public void CleanRequestHttpDataShouldIdentifiesRequestsByTheirIdentities()
        {
            // Create some data belonging to req1 and req2
            IAttribute attribute1 = this.factory.CreateAttribute(this.req1, "attribute1", "value1");
            IAttribute attribute2 = this.factory.CreateAttribute(this.req2, "attribute2", "value2");
            IFileUpload file1 = this.factory.CreateFileUpload(
                this.req1,
                "file1",
                "file1.txt",
                HttpPostBodyUtil.DefaultTextContentType,
                HttpHeaderValues.Identity.ToString(),
                Encoding.UTF8,
                123);

            IFileUpload file2 = this.factory.CreateFileUpload(
                this.req2,
                "file2",
                "file2.txt",
                HttpPostBodyUtil.DefaultTextContentType,
                HttpHeaderValues.Identity.ToString(),
                Encoding.UTF8,
                123);
            file1.SetContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("file1 content")));
            file2.SetContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("file2 content")));

            // Assert that they are not deleted
            Assert.NotNull(attribute1.GetByteBuffer());
            Assert.NotNull(attribute2.GetByteBuffer());
            Assert.NotNull(file1.GetByteBuffer());
            Assert.NotNull(file2.GetByteBuffer());
            Assert.Equal(1, attribute1.ReferenceCount);
            Assert.Equal(1, attribute2.ReferenceCount);
            Assert.Equal(1, file1.ReferenceCount);
            Assert.Equal(1, file2.ReferenceCount);

            // Clean up by req1
            this.factory.CleanRequestHttpData(this.req1);

            // Assert that data belonging to req1 has been cleaned up
            Assert.Null(attribute1.GetByteBuffer());
            Assert.Null(file1.GetByteBuffer());
            Assert.Equal(0, attribute1.ReferenceCount);
            Assert.Equal(0, file1.ReferenceCount);

            // But not req2
            Assert.NotNull(attribute2.GetByteBuffer());
            Assert.NotNull(file2.GetByteBuffer());
            Assert.Equal(1, attribute2.ReferenceCount);
            Assert.Equal(1, file2.ReferenceCount);
        }

        [Fact]
        public void RemoveHttpDataFromCleanShouldIdentifiesDataByTheirIdentities()
        {
            // Create some equal data items belonging to the same request
            IAttribute attribute1 = this.factory.CreateAttribute(this.req1, "attribute", "value");
            IAttribute attribute2 = this.factory.CreateAttribute(this.req1, "attribute", "value");
            IFileUpload file1 = this.factory.CreateFileUpload(
                this.req1,
                "file",
                "file.txt",
                HttpPostBodyUtil.DefaultTextContentType,
                HttpHeaderValues.Identity.ToString(),
                Encoding.UTF8,
                123);
            IFileUpload file2 = this.factory.CreateFileUpload(
                this.req1,
                "file",
                "file.txt",
                HttpPostBodyUtil.DefaultTextContentType,
                HttpHeaderValues.Identity.ToString(),
                Encoding.UTF8,
                123);
            file1.SetContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("file content")));
            file2.SetContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("file content")));

            // Before doing anything, assert that the data items are equal
            Assert.Equal(attribute1.GetHashCode(), attribute2.GetHashCode());
            Assert.True(attribute1.Equals(attribute2));
            Assert.Equal(file1.GetHashCode(), file2.GetHashCode());
            Assert.True(file1.Equals(file2));

            // Remove attribute2 and file2 from being cleaned up by factory
            this.factory.RemoveHttpDataFromClean(this.req1, attribute2);
            this.factory.RemoveHttpDataFromClean(this.req1, file2);

            // Clean up by req1
            this.factory.CleanRequestHttpData(this.req1);

            // Assert that attribute1 and file1 have been cleaned up
            Assert.Null(attribute1.GetByteBuffer());
            Assert.Null(file1.GetByteBuffer());
            Assert.Equal(0, attribute1.ReferenceCount);
            Assert.Equal(0, file1.ReferenceCount);

            // But not attribute2 and file2
            Assert.NotNull(attribute2.GetByteBuffer());
            Assert.NotNull(file2.GetByteBuffer());
            Assert.Equal(1, attribute2.ReferenceCount);
            Assert.Equal(1, file2.ReferenceCount);

            // Cleanup attribute2 and file2 manually to avoid memory leak, not via factory
            attribute2.Release();
            file2.Release();
            Assert.Equal(0, attribute2.ReferenceCount);
            Assert.Equal(0, file2.ReferenceCount);
        }

        public void Dispose() => this.factory.CleanAllHttpData();
    }
}
