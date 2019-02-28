// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using DotNetty.Common.Utilities;

    public class HttpResponseDecoder : HttpObjectDecoder
    {
        static readonly HttpResponseStatus UnknownStatus = new HttpResponseStatus(999, new AsciiString("Unknown"));

        public HttpResponseDecoder()
        {
        }

        public HttpResponseDecoder(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize) 
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, true)
        {
        }

        public HttpResponseDecoder(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders)
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, true, validateHeaders)
        {
        }

        public HttpResponseDecoder(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders, int initialBufferSize)
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, true, validateHeaders, initialBufferSize)
        {
        }

        protected sealed override IHttpMessage CreateMessage(AsciiString[] initialLine) =>
             new DefaultHttpResponse(
                HttpVersion.ValueOf(initialLine[0]),
                HttpResponseStatus.ValueOf(initialLine[1].ParseInt() , initialLine[2]), this.ValidateHeaders);

        protected override IHttpMessage CreateInvalidMessage() => new DefaultFullHttpResponse(HttpVersion.Http10, UnknownStatus, this.ValidateHeaders);

        protected override bool IsDecodingRequest() => false;
    }
}
