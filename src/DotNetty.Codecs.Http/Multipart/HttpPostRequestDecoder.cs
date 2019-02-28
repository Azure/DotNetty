// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;
    using DotNetty.Common.Utilities;

    public class HttpPostRequestDecoder : IInterfaceHttpPostRequestDecoder
    {
        internal static readonly int DefaultDiscardThreshold = 10 * 1024 * 1024;

        readonly IInterfaceHttpPostRequestDecoder decoder;

        public HttpPostRequestDecoder(IHttpRequest request)
            : this(new DefaultHttpDataFactory(DefaultHttpDataFactory.MinSize), request, HttpConstants.DefaultEncoding)
        {
        }

        public HttpPostRequestDecoder(IHttpDataFactory factory, IHttpRequest request)
            : this(factory, request, HttpConstants.DefaultEncoding)
        {
        }

        public HttpPostRequestDecoder(IHttpDataFactory factory, IHttpRequest request, Encoding encoding)
        {
            Contract.Requires(factory != null);
            Contract.Requires(request != null);
            Contract.Requires(encoding != null);

            // Fill default values
            if (IsMultipartRequest(request))
            {
                this.decoder = new HttpPostMultipartRequestDecoder(factory, request, encoding);
            }
            else
            {
                this.decoder = new HttpPostStandardRequestDecoder(factory, request, encoding);
            }
        }

        public static bool IsMultipartRequest(IHttpRequest request)
        {
            if (request.Headers.TryGet(HttpHeaderNames.ContentType, out ICharSequence contentType))
            {
                return GetMultipartDataBoundary(contentType) != null;
            }
            else
            {
                return false;
            }
        }

        // 
        // Check from the request ContentType if this request is a Multipart request.
        // return an array of String if multipartDataBoundary exists with the multipartDataBoundary
        // as first element, charset if any as second (missing if not set), else null
        //
        protected internal static ICharSequence[] GetMultipartDataBoundary(ICharSequence contentType)
        {
            // Check if Post using "multipart/form-data; boundary=--89421926422648 [; charset=xxx]"
            ICharSequence[] headerContentType = SplitHeaderContentType(contentType);
            AsciiString multiPartHeader = HttpHeaderValues.MultipartFormData;
            if (headerContentType[0].RegionMatchesIgnoreCase(0, multiPartHeader, 0, multiPartHeader.Count))
            {
                int mrank;
                int crank;
                AsciiString boundaryHeader = HttpHeaderValues.Boundary;
                if (headerContentType[1].RegionMatchesIgnoreCase(0, boundaryHeader, 0, boundaryHeader.Count))
                {
                    mrank = 1;
                    crank = 2;
                }
                else if (headerContentType[2].RegionMatchesIgnoreCase(0, boundaryHeader, 0, boundaryHeader.Count))
                {
                    mrank = 2;
                    crank = 1;
                }
                else
                {
                    return null;
                }
                ICharSequence boundary = headerContentType[mrank].SubstringAfter('=');
                if (boundary == null)
                {
                    throw new ErrorDataDecoderException("Needs a boundary value");
                }
                if (boundary[0] == '"')
                {
                    ICharSequence bound = CharUtil.Trim(boundary);
                    int index = bound.Count - 1;
                    if (bound[index] == '"')
                    {
                        boundary = bound.SubSequence(1, index);
                    }
                }
                AsciiString charsetHeader = HttpHeaderValues.Charset;
                if (headerContentType[crank].RegionMatchesIgnoreCase(0, charsetHeader, 0, charsetHeader.Count))
                {
                    ICharSequence charset = headerContentType[crank].SubstringAfter('=');
                    if (charset != null)
                    {
                        return new []
                        {
                            new StringCharSequence("--" + boundary.ToString()),
                            charset
                        };
                    }
                }

                return new ICharSequence[]
                {
                    new StringCharSequence("--" + boundary.ToString())
                };
            }

            return null;
        }

        public bool IsMultipart => this.decoder.IsMultipart;

        public int DiscardThreshold
        {
            get => this.decoder.DiscardThreshold;
            set => this.decoder.DiscardThreshold = value;
        }

        public List<IInterfaceHttpData> GetBodyHttpDatas() => this.decoder.GetBodyHttpDatas();

        public List<IInterfaceHttpData> GetBodyHttpDatas(AsciiString name) => this.decoder.GetBodyHttpDatas(name);

        public IInterfaceHttpData GetBodyHttpData(AsciiString name) => this.decoder.GetBodyHttpData(name);

        public IInterfaceHttpPostRequestDecoder Offer(IHttpContent content) => this.decoder.Offer(content);

        public bool HasNext => this.decoder.HasNext;

        public IInterfaceHttpData Next() => this.decoder.Next();

        public IInterfaceHttpData CurrentPartialHttpData => this.decoder.CurrentPartialHttpData;

        public void Destroy() => this.decoder.Destroy();

        public void CleanFiles() => this.decoder.CleanFiles();

        public void RemoveHttpDataFromClean(IInterfaceHttpData data) => this.decoder.RemoveHttpDataFromClean(data);

        static ICharSequence[] SplitHeaderContentType(ICharSequence sb)
        {
            int aStart = HttpPostBodyUtil.FindNonWhitespace(sb, 0);
            int aEnd = sb.IndexOf(';');
            if (aEnd == -1)
            {
                return new [] { sb,  StringCharSequence.Empty, StringCharSequence.Empty };
            }
            int bStart = HttpPostBodyUtil.FindNonWhitespace(sb, aEnd + 1);
            if (sb[aEnd - 1] == ' ')
            {
                aEnd--;
            }
            int bEnd = sb.IndexOf(';', bStart);
            if (bEnd == -1)
            {
                bEnd = HttpPostBodyUtil.FindEndOfString(sb);
                return new [] { sb.SubSequence(aStart, aEnd), sb.SubSequence(bStart, bEnd), StringCharSequence.Empty };
            }
            int cStart = HttpPostBodyUtil.FindNonWhitespace(sb, bEnd + 1);
            if (sb[bEnd - 1] == ' ')
            {
                bEnd--;
            }
            int cEnd = HttpPostBodyUtil.FindEndOfString(sb);
            return new [] { sb.SubSequence(aStart, aEnd), sb.SubSequence(bStart, bEnd), sb.SubSequence(cStart, cEnd) };
        }
    }
}
