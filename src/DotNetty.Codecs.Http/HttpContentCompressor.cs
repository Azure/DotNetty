// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Codecs.Compression;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    public class HttpContentCompressor : HttpContentEncoder
    {
        static readonly AsciiString GZipString = AsciiString.Cached("gzip");
        static readonly AsciiString DeflateString = AsciiString.Cached("deflate");

        readonly int compressionLevel;
        readonly int windowBits;
        readonly int memLevel;

        IChannelHandlerContext handlerContext;

        public HttpContentCompressor() : this(6)
        {
        }

        public HttpContentCompressor(int compressionLevel) : this(compressionLevel, 15, 8)
        {
        }

        public HttpContentCompressor(int compressionLevel, int windowBits, int memLevel)
        {
            Contract.Requires(compressionLevel >= 0 && compressionLevel <= 9);
            Contract.Requires(windowBits >= 9 && windowBits <= 15);
            Contract.Requires(memLevel >= 1 && memLevel <= 9);

            this.compressionLevel = compressionLevel;
            this.windowBits = windowBits;
            this.memLevel = memLevel;
        }

        public override void HandlerAdded(IChannelHandlerContext context) => this.handlerContext = context;

        protected override Result BeginEncode(IHttpResponse headers, ICharSequence acceptEncoding)
        {
            if (headers.Headers.Contains(HttpHeaderNames.ContentEncoding))
            {
                // Content-Encoding was set, either as something specific or as the IDENTITY encoding
                // Therefore, we should NOT encode here
                return null;
            }

            ZlibWrapper? wrapper = this.DetermineWrapper(acceptEncoding);
            if (wrapper == null)
            {
                return null;
            }

            ICharSequence targetContentEncoding;
            switch (wrapper.Value)
            {
                case ZlibWrapper.Gzip:
                    targetContentEncoding = GZipString;
                    break;
                case ZlibWrapper.Zlib:
                    targetContentEncoding = DeflateString;
                    break;
                default:
                    throw new CodecException($"{wrapper.Value} not supported, only Gzip and Zlib are allowed.");
            }

            return new Result(targetContentEncoding,
              new EmbeddedChannel(
                  this.handlerContext.Channel.Id, 
                  this.handlerContext.Channel.Metadata.HasDisconnect,
                  this.handlerContext.Channel.Configuration,
                  ZlibCodecFactory.NewZlibEncoder(
                      wrapper.Value, this.compressionLevel, this.windowBits, this.memLevel)));
        }

        protected internal ZlibWrapper? DetermineWrapper(ICharSequence acceptEncoding)
        {
            float starQ = -1.0f;
            float gzipQ = -1.0f;
            float deflateQ = -1.0f;
            ICharSequence[] parts = CharUtil.Split(acceptEncoding, ',');
            foreach (ICharSequence encoding in parts)
            {
                float q = 1.0f;
                int equalsPos = encoding.IndexOf('=');
                if (equalsPos != -1)
                {
                    try
                    {
                        q = float.Parse(encoding.ToString(equalsPos + 1));
                    }
                    catch (FormatException)
                    {
                        // Ignore encoding
                        q = 0.0f;
                    }
                }
                
                if (CharUtil.Contains(encoding, '*'))
                {
                    starQ = q;
                }
                else if (AsciiString.Contains(encoding, GZipString) && q > gzipQ)
                {
                    gzipQ = q;
                }
                else if (AsciiString.Contains(encoding, DeflateString) && q > deflateQ)
                {
                    deflateQ = q;
                }
            }
            if (gzipQ > 0.0f || deflateQ > 0.0f)
            {
                return gzipQ >= deflateQ ? ZlibWrapper.Gzip : ZlibWrapper.Zlib;
            }
            if (starQ > 0.0f)
            {
                // ReSharper disable CompareOfFloatsByEqualityOperator
                if (gzipQ == -1.0f)
                {
                    return ZlibWrapper.Gzip;
                }
                if (deflateQ == -1.0f)
                {
                    return ZlibWrapper.Zlib;
                }
                // ReSharper restore CompareOfFloatsByEqualityOperator
            }
            return null;
        }
    }
}
