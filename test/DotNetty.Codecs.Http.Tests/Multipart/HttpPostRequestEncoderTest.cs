// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.Multipart;
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class HttpPostRequestEncoderTest : IDisposable
    {
        readonly List<IDisposable> files = new List<IDisposable>();

        [Fact]
        public void AllowedMethods()
        {
            FileStream fileStream = File.Open("./Multipart/file-01.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream);

            ShouldThrowExceptionIfNotAllowed(HttpMethod.Connect, fileStream);
            ShouldThrowExceptionIfNotAllowed(HttpMethod.Put, fileStream);
            ShouldThrowExceptionIfNotAllowed(HttpMethod.Post, fileStream);
            ShouldThrowExceptionIfNotAllowed(HttpMethod.Patch, fileStream);
            ShouldThrowExceptionIfNotAllowed(HttpMethod.Delete, fileStream);
            ShouldThrowExceptionIfNotAllowed(HttpMethod.Get, fileStream);
            ShouldThrowExceptionIfNotAllowed(HttpMethod.Head, fileStream);
            ShouldThrowExceptionIfNotAllowed(HttpMethod.Options, fileStream);
            Assert.Throws<ErrorDataEncoderException>(() => ShouldThrowExceptionIfNotAllowed(HttpMethod.Trace, fileStream));
        }

        static void ShouldThrowExceptionIfNotAllowed(HttpMethod method, FileStream fileStream)
        {
            fileStream.Position = 0; // Reset to the begining
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, method, "http://localhost");

            var encoder = new HttpPostRequestEncoder(request, true);
            encoder.AddBodyAttribute("foo", "bar");
            encoder.AddBodyFileUpload("quux", fileStream, "text/plain", false);

            string multipartDataBoundary = encoder.MultipartDataBoundary;
            string content = GetRequestBody(encoder);

            string expected = "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"foo\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": 3" + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain; charset=utf-8" + "\r\n" +
                "\r\n" +
                "bar" +
                "\r\n" +
                "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"quux\"; filename=\"file-01.txt\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 01" + StringUtil.Newline +
                "\r\n" +
                "--" + multipartDataBoundary + "--" + "\r\n";

            Assert.Equal(expected, content);
        }

        [Fact]
        public void SingleFileUploadNoName()
        {
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            var encoder = new HttpPostRequestEncoder(request, true);

            FileStream fileStream = File.Open("./Multipart/file-01.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream);
            encoder.AddBodyAttribute("foo", "bar");
            encoder.AddBodyFileUpload("quux", "", fileStream, "text/plain", false);

            string multipartDataBoundary = encoder.MultipartDataBoundary;
            string content = GetRequestBody(encoder);

            string expected = "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"foo\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": 3" + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain; charset=utf-8" + "\r\n" +
                "\r\n" +
                "bar" +
                "\r\n" +
                "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"quux\"\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 01" + StringUtil.Newline +
                "\r\n" +
                "--" + multipartDataBoundary + "--" + "\r\n";

            Assert.Equal(expected, content);
        }

        [Fact]
        public void MultiFileUploadInMixedMode()
        {
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            var encoder = new HttpPostRequestEncoder(request, true);

            FileStream fileStream1 = File.Open("./Multipart/file-01.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream1);
            FileStream fileStream2 = File.Open("./Multipart/file-02.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream2);

            encoder.AddBodyAttribute("foo", "bar");
            encoder.AddBodyFileUpload("quux", fileStream1, "text/plain", false);
            encoder.AddBodyFileUpload("quux", fileStream2, "text/plain", false);

            // We have to query the value of these two fields before finalizing
            // the request, which unsets one of them.
            string multipartDataBoundary = encoder.MultipartDataBoundary;
            string multipartMixedBoundary = encoder.MultipartMixedBoundary;
            string content = GetRequestBody(encoder);

            string expected = "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"foo\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": 3" + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain; charset=utf-8" + "\r\n" +
                "\r\n" +
                "bar" + "\r\n" +
                "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"quux\"" + "\r\n" +
                HttpHeaderNames.ContentType + ": multipart/mixed; boundary=" + multipartMixedBoundary + "\r\n" +
                "\r\n" +
                "--" + multipartMixedBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": attachment; filename=\"file-02.txt\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream1.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 01" + StringUtil.Newline +
                "\r\n" +
                "--" + multipartMixedBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": attachment; filename=\"file-02.txt\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream2.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 02" + StringUtil.Newline +
                "\r\n" +
                "--" + multipartMixedBoundary + "--" + "\r\n" +
                "--" + multipartDataBoundary + "--" + "\r\n";

            Assert.Equal(expected, content);
        }

        [Fact]
        public void MultiFileUploadInMixedModeNoName()
        {
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            var encoder = new HttpPostRequestEncoder(request, true);

            FileStream fileStream1 = File.Open("./Multipart/file-01.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream1);
            FileStream fileStream2 = File.Open("./Multipart/file-02.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream2);

            encoder.AddBodyAttribute("foo", "bar");
            encoder.AddBodyFileUpload("quux", "", fileStream1, "text/plain", false);
            encoder.AddBodyFileUpload("quux", "", fileStream2, "text/plain", false);

            // We have to query the value of these two fields before finalizing
            // the request, which unsets one of them.
            string multipartDataBoundary = encoder.MultipartDataBoundary;
            string multipartMixedBoundary = encoder.MultipartMixedBoundary;
            string content = GetRequestBody(encoder);

            string expected = "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"foo\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": 3" + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain; charset=utf-8" + "\r\n" +
                "\r\n" +
                "bar" + "\r\n" +
                "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"quux\"" + "\r\n" +
                HttpHeaderNames.ContentType + ": multipart/mixed; boundary=" + multipartMixedBoundary + "\r\n" +
                "\r\n" +
                "--" + multipartMixedBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": attachment\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream1.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 01" + StringUtil.Newline +
                "\r\n" +
                "--" + multipartMixedBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": attachment\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream2.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 02" + StringUtil.Newline +
                "\r\n" +
                "--" + multipartMixedBoundary + "--" + "\r\n" +
                "--" + multipartDataBoundary + "--" + "\r\n";

            Assert.Equal(expected, content);
        }

        [Fact]
        public void SingleFileUploadInHtml5Mode()
        {
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            var factory = new DefaultHttpDataFactory(DefaultHttpDataFactory.MinSize);
            var encoder = new HttpPostRequestEncoder(
                factory,
                request,
                true,
                Encoding.UTF8,
                HttpPostRequestEncoder.EncoderMode.HTML5);

            FileStream fileStream1 = File.Open("./Multipart/file-01.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream1);
            FileStream fileStream2 = File.Open("./Multipart/file-02.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream2);

            encoder.AddBodyAttribute("foo", "bar");
            encoder.AddBodyFileUpload("quux", fileStream1, "text/plain", false);
            encoder.AddBodyFileUpload("quux", fileStream2, "text/plain", false);

            string multipartDataBoundary = encoder.MultipartDataBoundary;
            string content = GetRequestBody(encoder);

            string expected = "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"foo\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": 3" + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain; charset=utf-8" + "\r\n" +
                "\r\n" +
                "bar" + "\r\n" +
                "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"quux\"; filename=\"file-01.txt\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream1.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 01" + StringUtil.Newline + "\r\n" +
                "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"quux\"; filename=\"file-02.txt\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream2.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 02" + StringUtil.Newline +
                "\r\n" +
                "--" + multipartDataBoundary + "--" + "\r\n";

            Assert.Equal(expected, content);
        }

        [Fact]
        public void MultiFileUploadInHtml5Mode()
        {
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            var factory = new DefaultHttpDataFactory(DefaultHttpDataFactory.MinSize);

            var encoder = new HttpPostRequestEncoder(
                factory,
                request,
                true,
                Encoding.UTF8,
                HttpPostRequestEncoder.EncoderMode.HTML5);
            FileStream fileStream1 = File.Open("./Multipart/file-01.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream1);

            encoder.AddBodyAttribute("foo", "bar");
            encoder.AddBodyFileUpload("quux", fileStream1, "text/plain", false);

            string multipartDataBoundary = encoder.MultipartDataBoundary;
            string content = GetRequestBody(encoder);

            string expected = "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"foo\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": 3" + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain; charset=utf-8" + "\r\n" +
                "\r\n" +
                "bar" +
                "\r\n" +
                "--" + multipartDataBoundary + "\r\n" +
                HttpHeaderNames.ContentDisposition + ": form-data; name=\"quux\"; filename=\"file-01.txt\"" + "\r\n" +
                HttpHeaderNames.ContentLength + ": " + fileStream1.Length + "\r\n" +
                HttpHeaderNames.ContentType + ": text/plain" + "\r\n" +
                HttpHeaderNames.ContentTransferEncoding + ": binary" + "\r\n" +
                "\r\n" +
                "File 01" + StringUtil.Newline +
                "\r\n" +
                "--" + multipartDataBoundary + "--" + "\r\n";

            Assert.Equal(expected, content);
        }

        [Fact]
        public void HttpPostRequestEncoderSlicedBuffer()
        {
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");

            var encoder = new HttpPostRequestEncoder(request, true);
            // add Form attribute
            encoder.AddBodyAttribute("getform", "POST");
            encoder.AddBodyAttribute("info", "first value");
            encoder.AddBodyAttribute("secondinfo", "secondvalue a&");
            encoder.AddBodyAttribute("thirdinfo", "short text");

            const int Length = 100000;
            var array = new char[Length];
            array.Fill('a');
            string longText = new string(array);
            encoder.AddBodyAttribute("fourthinfo", longText.Substring(0, 7470));

            FileStream fileStream1 = File.Open("./Multipart/file-01.txt", FileMode.Open, FileAccess.Read);
            this.files.Add(fileStream1);
            encoder.AddBodyFileUpload("myfile", fileStream1, "application/x-zip-compressed", false);
            encoder.FinalizeRequest();

            while (!encoder.IsEndOfInput)
            {
                IHttpContent httpContent = encoder.ReadChunk(null);
                IByteBuffer content = httpContent.Content;
                int refCnt = content.ReferenceCount;
                Assert.True(
                    (ReferenceEquals(content.Unwrap(), content) || content.Unwrap() == null) && refCnt == 1
                    || !ReferenceEquals(content.Unwrap(), content) && refCnt == 2,
                    "content: " + content + " content.unwrap(): " + content.Unwrap() + " refCnt: " + refCnt);
                httpContent.Release();
            }

            encoder.CleanFiles();
            encoder.Close();
        }

        static string GetRequestBody(HttpPostRequestEncoder encoder)
        {
            encoder.FinalizeRequest();

            List<IInterfaceHttpData> chunks = encoder.MultipartHttpDatas;
            var buffers = new IByteBuffer[chunks.Count];

            for (int i = 0; i < buffers.Length; i++)
            {
                IInterfaceHttpData data = chunks[i];
                if (data is InternalAttribute attribute)
                {
                    buffers[i] = attribute.ToByteBuffer();
                }
                else if (data is IHttpData httpData)
                {
                    buffers[i] = httpData.GetByteBuffer();
                }
            }

            IByteBuffer content = Unpooled.WrappedBuffer(buffers);
            string contentStr = content.ToString(Encoding.UTF8);
            content.Release();
            return contentStr;
        }

        [Fact]
        public void DataIsMultipleOfChunkSize1()
        {
            var factory = new DefaultHttpDataFactory(DefaultHttpDataFactory.MinSize);
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            var encoder = new HttpPostRequestEncoder(factory, request, true,
                HttpConstants.DefaultEncoding, HttpPostRequestEncoder.EncoderMode.RFC1738);

            var first = new MemoryFileUpload("resources", "", "application/json", null, Encoding.UTF8, -1);
            first.MaxSize = -1;
            first.SetContent(new MemoryStream(new byte[7955]));
            encoder.AddBodyHttpData(first);

            var second = new MemoryFileUpload("resources2", "", "application/json", null, Encoding.UTF8, -1);
            second.MaxSize = -1;
            second.SetContent(new MemoryStream(new byte[7928]));
            encoder.AddBodyHttpData(second);

            Assert.NotNull(encoder.FinalizeRequest());

            CheckNextChunkSize(encoder, 8080);
            CheckNextChunkSize(encoder, 8055);

            IHttpContent httpContent = encoder.ReadChunk(default(IByteBufferAllocator));
            Assert.True(httpContent is ILastHttpContent, "Expected LastHttpContent is not received");
            httpContent.Release();

            Assert.True(encoder.IsEndOfInput, "Expected end of input is not receive");
        }

        [Fact]
        public void DataIsMultipleOfChunkSize2()
        {
            var request = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            var encoder = new HttpPostRequestEncoder(request, true);
            const int Length = 7943;
            var array = new char[Length];
            array.Fill('a');
            string longText = new string(array);
            encoder.AddBodyAttribute("foo", longText);

            Assert.NotNull(encoder.FinalizeRequest());

            // In Netty this is 8080 due to random long hex size difference
            CheckNextChunkSize(encoder, 109 + Length + 8);

            IHttpContent httpContent = encoder.ReadChunk(default(IByteBufferAllocator));
            Assert.True(httpContent is ILastHttpContent, "Expected LastHttpContent is not received");
            httpContent.Release();

            Assert.True(encoder.IsEndOfInput, "Expected end of input is not receive");
        }

        static void CheckNextChunkSize(HttpPostRequestEncoder encoder, int sizeWithoutDelimiter)
        {
            // 16 bytes as HttpPostRequestEncoder uses Long.toHexString(...) to generate a hex-string which will be between
            // 2 and 16 bytes.
            // See https://github.com/netty/netty/blob/4.1/codec-http/src/main/java/io/netty/handler/
            // codec/http/multipart/HttpPostRequestEncoder.java#L291
            int expectedSizeMin = sizeWithoutDelimiter + (2 + 2);   // Two multipar boundary strings
            int expectedSizeMax = sizeWithoutDelimiter + (16 + 16); // Two multipar boundary strings

            IHttpContent httpContent = encoder.ReadChunk(default(IByteBufferAllocator));

            int readable = httpContent.Content.ReadableBytes;
            bool expectedSize = readable >= expectedSizeMin && readable <= expectedSizeMax;
            Assert.True(expectedSize, $"Chunk size is not in expected range ({expectedSizeMin} - {expectedSizeMax}), was: {readable}");
            httpContent.Release();
        }

        public void Dispose()
        {
            foreach (IDisposable file in this.files)
            {
                file.Dispose();
            }
        }
    }
}
