// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.Multipart;
    using DotNetty.Common.Utilities;
    using Xunit;

    public sealed class HttpPostRequestDecoderTest
    {
        // https://github.com/netty/netty/issues/1575
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BinaryStreamUpload(bool withSpace)
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";
            string contentTypeValue;
            if (withSpace)
            {
                contentTypeValue = "multipart/form-data; boundary=" + Boundary;
            }
            else
            {
                contentTypeValue = "multipart/form-data;boundary=" + Boundary;
            }
            var req = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            req.Result = DecoderResult.Success;
            req.Headers.Add(HttpHeaderNames.ContentType, contentTypeValue);
            req.Headers.Add(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);

            var values = new[] { "", "\r", "\r\r", "\r\r\r" };
            foreach (string data in values)
            {
                string body =
                    "--" + Boundary + "\r\n" +
                    "Content-Disposition: form-data; name=\"file\"; filename=\"tmp-0.txt\"\r\n" +
                    "Content-Type: image/gif\r\n" +
                    "\r\n" +
                    data + "\r\n" +
                    "--" + Boundary + "--\r\n";

                // Create decoder instance to test.
                var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);

                decoder.Offer(new DefaultHttpContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(body))));
                decoder.Offer(new DefaultHttpContent(Unpooled.Empty));

                // Validate it's enough chunks to decode upload.
                Assert.True(decoder.HasNext);

                // Decode binary upload.
                IInterfaceHttpData next = decoder.Next();
                Assert.IsType<MemoryFileUpload>(next);
                var upload = (MemoryFileUpload)next;

                // Validate data has been parsed correctly as it was passed into request.
                Assert.Equal(data, upload.GetString(Encoding.UTF8));
                upload.Release();
                decoder.Destroy();
            }
        }

        // See https://github.com/netty/netty/issues/1089
        [Fact]
        public void FullHttpRequestUpload()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";

            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            req.Result = DecoderResult.Success;
            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            req.Headers.Add(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);

            var values = new[] { "", "\r", "\r\r", "\r\r\r" };
            foreach (string data in values)
            {
                string body =
                        "--" + Boundary + "\r\n" +
                                "Content-Disposition: form-data; name=\"file\"; filename=\"tmp-0.txt\"\r\n" +
                                "Content-Type: image/gif\r\n" +
                                "\r\n" +
                                data + "\r\n" +
                                "--" + Boundary + "--\r\n";

                req.Content.WriteBytes(Encoding.UTF8.GetBytes(body));
            }

            // Create decoder instance to test.
            var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
            List<IInterfaceHttpData> list = decoder.GetBodyHttpDatas();
            Assert.NotNull(list);
            Assert.False(list.Count == 0);
            decoder.Destroy();
        }

        // See https://github.com/netty/netty/issues/2544
        [Fact]
        public void MultipartCodecWithCRasEndOfAttribute()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";

            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);

            const string Extradata = "aaaa";
            var strings = new string[5];
            for (int i = 0; i < 4; i++)
            {
                strings[i] = Extradata;
                for (int j = 0; j < i; j++)
                {
                    strings[i] += '\r';
                }
            }

            for (int i = 0; i < 4; i++)
            {
                var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
                req.Result = DecoderResult.Success;
                req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
                req.Headers.Add(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
                string body =
                        "--" + Boundary + "\r\n" +
                                "Content-Disposition: form-data; name=\"file" + i + "\"\r\n" +
                                "Content-Type: image/gif\r\n" +
                                "\r\n" +
                                strings[i] + "\r\n" +
                                "--" + Boundary + "--\r\n";

                req.Content.WriteBytes(Encoding.UTF8.GetBytes(body));
                // Create decoder instance to test.
                var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
                List<IInterfaceHttpData> list = decoder.GetBodyHttpDatas();
                Assert.NotNull(list);
                Assert.False(list.Count == 0);

                // Check correctness: data size
                IInterfaceHttpData httpData = decoder.GetBodyHttpData(new AsciiString($"file{i}"));
                Assert.NotNull(httpData);
                var attribute = httpData as IAttribute;
                Assert.NotNull(attribute);

                byte[] data = attribute.GetBytes();
                Assert.NotNull(data);
                Assert.Equal(Encoding.UTF8.GetBytes(strings[i]).Length, data.Length);

                decoder.Destroy();
            }
        }

        // See https://github.com/netty/netty/issues/2542
        [Fact]
        public void QuotedBoundary()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";

            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");

            req.Result = DecoderResult.Success;
            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=\"" + Boundary + '"');
            req.Headers.Add(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);

            var values = new[] { "", "\r", "\r\r", "\r\r\r" };
            foreach (string data in values)
            {
                string body =
                    "--" + Boundary + "\r\n" +
                    "Content-Disposition: form-data; name=\"file\"; filename=\"tmp-0.txt\"\r\n" +
                    "Content-Type: image/gif\r\n" +
                    "\r\n" +
                    data + "\r\n" +
                    "--" + Boundary + "--\r\n";

                req.Content.WriteBytes(Encoding.UTF8.GetBytes(body));
            }

            // Create decoder instance to test.
            var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
            List<IInterfaceHttpData> list = decoder.GetBodyHttpDatas();
            Assert.NotNull(list);
            Assert.False(list.Count == 0);
            decoder.Destroy();
        }

        [Fact]
        public void NoZeroOut()
        {
            const string Boundary = "E832jQp_Rq2ErFmAduHSR8YlMSm0FCY";

            var aMemFactory = new DefaultHttpDataFactory(false);
            var aRequest = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            aRequest.Headers.Set(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            aRequest.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

            var aDecoder = new HttpPostRequestDecoder(aMemFactory, aRequest);

            const string BodyData = "some data would be here. the data should be long enough that it " +
                "will be longer than the original buffer length of 256 bytes in " +
                "the HttpPostRequestDecoder in order to trigger the issue. Some more " +
                "data just to be on the safe side.";

            const string Body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"root\"\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n" +
                BodyData +
                "\r\n" +
                "--" + Boundary + "--\r\n";

            byte[] aBytes = Encoding.UTF8.GetBytes(Body);
            const int Split = 125;

            UnpooledByteBufferAllocator aAlloc = UnpooledByteBufferAllocator.Default;
            IByteBuffer aSmallBuf = aAlloc.Buffer(Split, Split);
            IByteBuffer aLargeBuf = aAlloc.Buffer(aBytes.Length - Split, aBytes.Length - Split);

            aSmallBuf.WriteBytes(aBytes, 0, Split);
            aLargeBuf.WriteBytes(aBytes, Split, aBytes.Length - Split);

            aDecoder.Offer(new DefaultHttpContent(aSmallBuf));
            aDecoder.Offer(new DefaultHttpContent(aLargeBuf));
            aDecoder.Offer(EmptyLastHttpContent.Default);

            Assert.True(aDecoder.HasNext);
            IInterfaceHttpData aDecodedData = aDecoder.Next();
            Assert.Equal(HttpDataType.Attribute, aDecodedData.DataType);

            var aAttr = (IAttribute)aDecodedData;
            Assert.Equal(BodyData, aAttr.Value);

            aDecodedData.Release();
            aDecoder.Destroy();
        }

        // See https://github.com/netty/netty/issues/2305
        [Fact]
        public void ChunkCorrect()
        {
            const string Payload = "town=794649819&town=784444184&town=794649672&town=794657800&town=" +
                "794655734&town=794649377&town=794652136&town=789936338&town=789948986&town=" +
                "789949643&town=786358677&town=794655880&town=786398977&town=789901165&town=" +
                "789913325&town=789903418&town=789903579&town=794645251&town=794694126&town=" +
                "794694831&town=794655274&town=789913656&town=794653956&town=794665634&town=" +
                "789936598&town=789904658&town=789899210&town=799696252&town=794657521&town=" +
                "789904837&town=789961286&town=789958704&town=789948839&town=789933899&town=" +
                "793060398&town=794659180&town=794659365&town=799724096&town=794696332&town=" +
                "789953438&town=786398499&town=794693372&town=789935439&town=794658041&town=" +
                "789917595&town=794655427&town=791930372&town=794652891&town=794656365&town=" +
                "789960339&town=794645586&town=794657688&town=794697211&town=789937427&town=" +
                "789902813&town=789941130&town=794696907&town=789904328&town=789955151&town=" +
                "789911570&town=794655074&town=789939531&town=789935242&town=789903835&town=" +
                "789953800&town=794649962&town=789939841&town=789934819&town=789959672&town=" +
                "794659043&town=794657035&town=794658938&town=794651746&town=794653732&town=" +
                "794653881&town=786397909&town=794695736&town=799724044&town=794695926&town=" +
                "789912270&town=794649030&town=794657946&town=794655370&town=794659660&town=" +
                "794694617&town=799149862&town=789953234&town=789900476&town=794654995&town=" +
                "794671126&town=789908868&town=794652942&town=789955605&town=789901934&town=" +
                "789950015&town=789937922&town=789962576&town=786360170&town=789954264&town=" +
                "789911738&town=789955416&town=799724187&town=789911879&town=794657462&town=" +
                "789912561&town=789913167&town=794655195&town=789938266&town=789952099&town=" +
                "794657160&town=789949414&town=794691293&town=794698153&town=789935636&town=" +
                "789956374&town=789934635&town=789935475&town=789935085&town=794651425&town=" +
                "794654936&town=794655680&town=789908669&town=794652031&town=789951298&town=" +
                "789938382&town=794651503&town=794653330&town=817675037&town=789951623&town=" +
                "789958999&town=789961555&town=794694050&town=794650241&town=794656286&town=" +
                "794692081&town=794660090&town=794665227&town=794665136&town=794669931";

            var defaultHttpRequest = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/");

            var decoder = new HttpPostRequestDecoder(defaultHttpRequest);

            const int FirstChunk = 10;
            const int MiddleChunk = 1024;

            var part1 = new DefaultHttpContent(Unpooled.WrappedBuffer(
                Encoding.UTF8.GetBytes(Payload.Substring(0, FirstChunk))));
            var part2 = new DefaultHttpContent(Unpooled.WrappedBuffer(
                Encoding.UTF8.GetBytes(Payload.Substring(FirstChunk, MiddleChunk))));
            var part3 = new DefaultHttpContent(Unpooled.WrappedBuffer(
                Encoding.UTF8.GetBytes(Payload.Substring(FirstChunk + MiddleChunk, MiddleChunk))));
            var part4 = new DefaultHttpContent(Unpooled.WrappedBuffer(
                Encoding.UTF8.GetBytes(Payload.Substring(FirstChunk + MiddleChunk * 2))));

            decoder.Offer(part1);
            decoder.Offer(part2);
            decoder.Offer(part3);
            decoder.Offer(part4);
        }

        // See https://github.com/netty/netty/issues/3326
        [Fact]
        public void FilenameContainingSemicolon()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);

            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);

            const string Data = "asdf";
            const string Filename = "tmp;0.txt";
            const string Body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename=\"" + Filename + "\"\r\n" +
                "Content-Type: image/gif\r\n" +
                "\r\n" +
                Data + "\r\n" +
                "--" + Boundary + "--\r\n";

            req.Content.WriteBytes(Encoding.UTF8.GetBytes(Body));
            var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
            List<IInterfaceHttpData> list = decoder.GetBodyHttpDatas();
            Assert.NotNull(list);
            Assert.False(list.Count == 0);
            decoder.Destroy();
        }

        [Fact]
        public void FilenameContainingSemicolon2()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);

            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);
            const string Data = "asdf";
            const string Filename = "tmp;0.txt";
            const string Body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename=\"" + Filename + "\"\r\n" +
                "Content-Type: image/gif\r\n" +
                "\r\n" +
                Data + "\r\n" +
                "--" + Boundary + "--\r\n";

            req.Content.WriteBytes(Encoding.UTF8.GetBytes(Body));
            var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
            List<IInterfaceHttpData> list = decoder.GetBodyHttpDatas();
            Assert.NotNull(list);
            Assert.False(list.Count == 0);

            IInterfaceHttpData part1 = list[0];
            Assert.IsAssignableFrom<IFileUpload>(part1);
            var fileUpload = (IFileUpload)part1;
            Assert.Equal("tmp 0.txt", fileUpload.FileName);
            decoder.Destroy();
        }

        [Fact]
        public void MultipartRequestWithoutContentTypeBody()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";

            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            req.Result = DecoderResult.Success;
            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            req.Headers.Add(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);

            var values = new[] { "", "\r", "\r\r", "\r\r\r" };
            foreach (string data in values)
            {
                string body =
                    "--" + Boundary + "\r\n" +
                    "Content-Disposition: form-data; name=\"file\"; filename=\"tmp-0.txt\"\r\n" +
                    "\r\n" +
                    data + "\r\n" +
                    "--" + Boundary + "--\r\n";

                req.Content.WriteBytes(Encoding.UTF8.GetBytes(body));
            }

            // Create decoder instance to test without any exception.
            var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
            List<IInterfaceHttpData> list = decoder.GetBodyHttpDatas();
            Assert.NotNull(list);
            Assert.False(list.Count == 0);
            decoder.Destroy();
        }

        [Fact]
        public void MultipartRequestWithFileInvalidCharset()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);

            const string Data = "asdf";
            const string FileName = "tmp;0.txt";
            string body =
                "--" + Boundary + "\r\n" +
                    "Content-Disposition: form-data; name=\"file\"; filename=\"" + FileName + "\"\r\n" +
                    "Content-Type: image/gif; charset=ABCD\r\n" +
                    "\r\n" +
                    Data + "\r\n" +
                    "--" + Boundary + "--\r\n";

            req.Content.WriteBytes(Encoding.UTF8.GetBytes(body));
            Assert.Throws<ErrorDataDecoderException>(() => new HttpPostRequestDecoder(inMemoryFactory, req));
        }

        [Fact]
        public void MultipartRequestWithFieldInvalidCharset()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";
            var req = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, "http://localhost");
            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);

            const string BodyData = "some data would be here. the data should be long enough that it " +
                "will be longer than the original buffer length of 256 bytes in " +
                "the HttpPostRequestDecoder in order to trigger the issue. Some more " +
                "data just to be on the safe side.";

            const string Body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"root\"\r\n" +
                "Content-Type: text/plain; charset=ABCD\r\n" +
                "\r\n" +
                BodyData +
                "\r\n" +
                "--" + Boundary + "--\r\n";

            req.Content.WriteBytes(Encoding.UTF8.GetBytes(Body));
            Assert.Throws<ErrorDataDecoderException>(() => new HttpPostRequestDecoder(inMemoryFactory, req));
        }

        [Fact]
        public void FormEncodeIncorrect()
        {
            var content = new DefaultLastHttpContent(Unpooled.CopiedBuffer(
                Encoding.ASCII.GetBytes("project=netty&&project=netty")));
            var req = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/");
            var decoder = new HttpPostRequestDecoder(req);
            Assert.Throws<ErrorDataDecoderException>(() => decoder.Offer(content));
            decoder.Destroy();
            content.Release();
        }

        [Fact]
        public void DecodeContentDispositionFieldParameters()
        {
            const string Boundary = "74e78d11b0214bdcbc2f86491eeb4902";
            const string Charset = "utf-8";
            const string Filename = "attached_файл.txt";
            string filenameEncoded = UrlEncoder.Encode(Filename, Encoding.UTF8);

            string body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename*=" + Charset + "''" + filenameEncoded + "\r\n" +
                "\r\n" +
                "foo\r\n" +
                "\r\n" +
                "--" + Boundary + "--";

            var req = new DefaultFullHttpRequest(HttpVersion.Http11,
                HttpMethod.Post,
                "http://localhost",
                Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(body)));

            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            var inMemoryFactory = new DefaultHttpDataFactory(false);
            var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
            Assert.False(decoder.GetBodyHttpDatas().Count == 0);
            IInterfaceHttpData part1 = decoder.GetBodyHttpDatas()[0];
            Assert.IsAssignableFrom<IFileUpload>(part1);

            var fileUpload = (IFileUpload)part1;
            Assert.Equal(Filename, fileUpload.FileName);
            decoder.Destroy();
            req.Release();
        }

        [Fact]
        public void DecodeWithLanguageContentDispositionFieldParameters()
        {
            const string Boundary = "74e78d11b0214bdcbc2f86491eeb4902";
            const string Charset = "utf-8";
            const string Filename = "attached_файл.txt";
            const string Language = "anything";
            string filenameEncoded = UrlEncoder.Encode(Filename, Encoding.UTF8);

            string body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename*=" +
                Charset + "'" + Language + "'" + filenameEncoded + "\r\n" +
                "\r\n" +
                "foo\r\n" +
                "\r\n" +
                "--" + Boundary + "--";

            var req = new DefaultFullHttpRequest(
                HttpVersion.Http11,
                HttpMethod.Post,
                "http://localhost",
                Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(body)));

            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            var inMemoryFactory = new DefaultHttpDataFactory(false);
            var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
            Assert.False(decoder.GetBodyHttpDatas().Count == 0);
            IInterfaceHttpData part1 = decoder.GetBodyHttpDatas()[0];
            Assert.IsAssignableFrom<IFileUpload>(part1);
            var fileUpload = (IFileUpload)part1;
            Assert.Equal(Filename, fileUpload.FileName);
            decoder.Destroy();
            req.Release();
        }

        [Fact]
        public void DecodeMalformedNotEncodedContentDispositionFieldParameters()
        {
            const string Boundary = "74e78d11b0214bdcbc2f86491eeb4902";

            const string Body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename*=not-encoded\r\n" +
                "\r\n" +
                "foo\r\n" +
                "\r\n" +
                "--" + Boundary + "--";

            var req = new DefaultFullHttpRequest(
                HttpVersion.Http11,
                HttpMethod.Post,
                "http://localhost",
                Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(Body)));

            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            var inMemoryFactory = new DefaultHttpDataFactory(false);
            Assert.Throws<ErrorDataDecoderException>(() => new HttpPostRequestDecoder(inMemoryFactory, req));
            req.Release();
        }

        [Fact]
        public void DecodeMalformedBadCharsetContentDispositionFieldParameters()
        {
            const string Boundary = "74e78d11b0214bdcbc2f86491eeb4902";

            const string Body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename*=not-a-charset''filename\r\n" +
                "\r\n" +
                "foo\r\n" +
                "\r\n" +
                "--" + Boundary + "--";

            var req = new DefaultFullHttpRequest(
                HttpVersion.Http11,
                HttpMethod.Post,
                "http://localhost",
                Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(Body)));

            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);

            var inMemoryFactory = new DefaultHttpDataFactory(false);
            Assert.Throws<ErrorDataDecoderException>(() => new HttpPostRequestDecoder(inMemoryFactory, req));
            req.Release();
        }

        [Fact]
        public void DecodeMalformedEmptyContentTypeFieldParameters()
        {
            const string Boundary = "dLV9Wyq26L_-JQxk6ferf-RT153LhOO";
            var req = new DefaultFullHttpRequest(
                HttpVersion.Http11,
                HttpMethod.Post,
                "http://localhost");

            req.Headers.Add(HttpHeaderNames.ContentType, "multipart/form-data; boundary=" + Boundary);
            // Force to use memory-based data.
            var inMemoryFactory = new DefaultHttpDataFactory(false);
            const string Data = "asdf";
            const string Filename = "tmp-0.txt";
            const string Body = "--" + Boundary + "\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename=\"" + Filename + "\"\r\n" +
                "Content-Type: \r\n" +
                "\r\n" +
                Data + "\r\n" +
                "--" + Boundary + "--\r\n";

            req.Content.WriteBytes(Encoding.UTF8.GetBytes(Body));
            // Create decoder instance to test.
            var decoder = new HttpPostRequestDecoder(inMemoryFactory, req);
            Assert.False(decoder.GetBodyHttpDatas().Count == 0);
            IInterfaceHttpData part1 = decoder.GetBodyHttpDatas()[0];
            Assert.IsAssignableFrom<IFileUpload>(part1);
            var fileUpload = (IFileUpload)part1;
            Assert.Equal(Filename, fileUpload.FileName);
            decoder.Destroy();
        }
    }
}
