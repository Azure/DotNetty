// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    using static HttpConstants;

    public class HttpRequestEncoder : HttpObjectEncoder<IHttpRequest>
    {
        const char Slash = '/';
        const char QuestionMark = '?';
        const int SlashAndSpaceShort = (Slash << 8) | HorizontalSpace;
        const int SpaceSlashAndSpaceMedium = (HorizontalSpace << 16) | SlashAndSpaceShort;

        public override bool AcceptOutboundMessage(object msg) => base.AcceptOutboundMessage(msg) && !(msg is IHttpResponse);

        protected internal override void EncodeInitialLine(IByteBuffer buf, IHttpRequest request)
        {
            ByteBufferUtil.Copy(request.Method.AsciiName, buf);

            string uri = request.Uri;

            if (string.IsNullOrEmpty(uri))
            {
                // Add / as absolute path if no is present.
                // See http://tools.ietf.org/html/rfc2616#section-5.1.2
                buf.WriteMedium(SpaceSlashAndSpaceMedium);
            }
            else
            {
                var uriCharSequence = new StringBuilderCharSequence();
                uriCharSequence.Append(uri);

                bool needSlash = false;
                int start = uri.IndexOf("://", StringComparison.Ordinal);
                if (start != -1 && uri[0] != Slash)
                {
                    start += 3;
                    // Correctly handle query params.
                    // See https://github.com/netty/netty/issues/2732
                    int index = uri.IndexOf(QuestionMark, start);
                    if (index == -1)
                    {
                        if (uri.LastIndexOf(Slash) < start)
                        {
                            needSlash = true;
                        }
                    }
                    else
                    {
                        if (uri.LastIndexOf(Slash, index) < start)
                        {
                            uriCharSequence.Insert(index, Slash);
                        }
                    }
                }

                buf.WriteByte(HorizontalSpace).WriteCharSequence(uriCharSequence, Encoding.UTF8);
                if (needSlash)
                {
                    // write "/ " after uri
                    buf.WriteShort(SlashAndSpaceShort);
                }
                else
                {
                    buf.WriteByte(HorizontalSpace);
                }
            }

            request.ProtocolVersion.Encode(buf);
            buf.WriteShort(CrlfShort);
        }
    }
}
