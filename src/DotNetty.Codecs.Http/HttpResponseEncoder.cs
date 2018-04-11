// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using DotNetty.Buffers;

    public class HttpResponseEncoder : HttpObjectEncoder<IHttpResponse>
    {
        public override bool AcceptOutboundMessage(object msg) => base.AcceptOutboundMessage(msg) && !(msg is IHttpRequest);

        protected internal override void EncodeInitialLine(IByteBuffer buf, IHttpResponse response)
        {
            response.ProtocolVersion.Encode(buf);
            buf.WriteByte(HttpConstants.HorizontalSpace);
            response.Status.Encode(buf);
            buf.WriteShort(HttpConstants.CrlfShort);
        }

        protected override void SanitizeHeadersBeforeEncode(IHttpResponse msg, bool isAlwaysEmpty)
        {
            if (isAlwaysEmpty)
            {
                HttpResponseStatus status = msg.Status;
                if (status.CodeClass == HttpStatusClass.Informational 
                    || status.Code == HttpResponseStatus.NoContent.Code)
                {

                    // Stripping Content-Length:
                    // See https://tools.ietf.org/html/rfc7230#section-3.3.2
                    msg.Headers.Remove(HttpHeaderNames.ContentLength);

                    // Stripping Transfer-Encoding:
                    // See https://tools.ietf.org/html/rfc7230#section-3.3.1
                    msg.Headers.Remove(HttpHeaderNames.TransferEncoding);
                }
            }
        }

        protected override bool IsContentAlwaysEmpty(IHttpResponse msg)
        {
            // Correctly handle special cases as stated in:
            // https://tools.ietf.org/html/rfc7230#section-3.3.3
            HttpResponseStatus status = msg.Status;

            if (status.CodeClass == HttpStatusClass.Informational)
            {
                if (status.Code == HttpResponseStatus.SwitchingProtocols.Code)
                {
                    // We need special handling for WebSockets version 00 as it will include an body.
                    // Fortunally this version should not really be used in the wild very often.
                    // See https://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-00#section-1.2
                    return msg.Headers.Contains(HttpHeaderNames.SecWebsocketVersion);
                }
                return true;
            }
            return status.Code == HttpResponseStatus.NoContent.Code 
                || status.Code == HttpResponseStatus.NotModified.Code;
        }
    }
}
