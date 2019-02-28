// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using DotNetty.Common.Utilities;

    public class HttpRequestDecoder : HttpObjectDecoder
    {
        public HttpRequestDecoder()
        {
        }

        public HttpRequestDecoder(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize)
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, true)
        {
        }

        public HttpRequestDecoder(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders)
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, true, validateHeaders)
        {
        }

        public HttpRequestDecoder(
            int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders, 
            int initialBufferSize) 
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, true, validateHeaders, initialBufferSize)
        {
        }

        protected sealed override IHttpMessage CreateMessage(AsciiString[] initialLine) =>
            new DefaultHttpRequest(
                HttpVersion.ValueOf(initialLine[2]),
                HttpMethod.ValueOf(initialLine[0]), initialLine[1].ToString(), this.ValidateHeaders);

        protected override IHttpMessage CreateInvalidMessage() =>  new DefaultFullHttpRequest(HttpVersion.Http10, HttpMethod.Get, "/bad-request", this.ValidateHeaders);

        protected override bool IsDecodingRequest() => true;
    }
}
