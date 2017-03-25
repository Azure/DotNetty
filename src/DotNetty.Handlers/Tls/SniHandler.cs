// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Security;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    public sealed class SniHandler : ByteToMessageDecoder
    {
        const int MAX_SSL_RECORDS = 4;
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(SniHandler));
        readonly Func<Stream, SslStream> sslStreamFactory;
        readonly ServerTlsSniSettings serverTlsSniSettings;
        bool handlerReplaced;

        bool handshakeFailed;

        public SniHandler(Func<Stream, SslStream> sslStreamFactory, ServerTlsSniSettings settings)
        {
            this.sslStreamFactory = sslStreamFactory;
            this.serverTlsSniSettings = settings;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (!this.handshakeFailed)
            {
                int writerIndex = input.WriterIndex;
                try
                {
                    for (int i = 0; i < MAX_SSL_RECORDS; i++)
                    {
                        int readerIndex = input.ReaderIndex;
                        int readableBytes = writerIndex - readerIndex;
                        if (readableBytes < TlsUtils.SSL_RECORD_HEADER_LENGTH)
                        {
                            // Not enough data to determine the record type and length.
                            return;
                        }

                        int command = input.GetByte(readerIndex);
                        // tls, but not handshake command
                        switch (command)
                        {
                            case TlsUtils.SSL_CONTENT_TYPE_CHANGE_CIPHER_SPEC:
                            case TlsUtils.SSL_CONTENT_TYPE_ALERT:
                                int len = TlsUtils.GetEncryptedPacketLength(input, readerIndex);

                                // Not an SSL/TLS packet
                                if (len == TlsUtils.NOT_ENCRYPTED)
                                {
                                    this.handshakeFailed = true;
                                    NotSslRecordException e = new NotSslRecordException(
                                        "not an SSL/TLS record: " + ByteBufferUtil.HexDump(input));
                                    input.SkipBytes(input.ReadableBytes);

                                    TlsUtils.NotifyHandshakeFailure(context, e);
                                    throw e;
                                }
                                if (len == TlsUtils.NOT_ENOUGH_DATA ||
                                    writerIndex - readerIndex - TlsUtils.SSL_RECORD_HEADER_LENGTH < len)
                                {
                                    // Not enough data
                                    return;
                                }

                                // increase readerIndex and try again.
                                input.SkipBytes(len);
                                continue;

                            case TlsUtils.SSL_CONTENT_TYPE_HANDSHAKE:
                                int majorVersion = input.GetByte(readerIndex + 1);

                                // SSLv3 or TLS
                                if (majorVersion == 3)
                                {
                                    int packetLength = input.GetUnsignedShort(readerIndex + 3) + TlsUtils.SSL_RECORD_HEADER_LENGTH;

                                    if (readableBytes < packetLength)
                                    {
                                        // client hello incomplete; try again to decode once more data is ready.
                                        return;
                                    }

                                    // See https://tools.ietf.org/html/rfc5246#section-7.4.1.2
                                    //
                                    // Decode the ssl client hello packet.
                                    // We have to skip bytes until SessionID (which sum to 43 bytes).
                                    //
                                    // struct {
                                    //    ProtocolVersion client_version;
                                    //    Random random;
                                    //    SessionID session_id;
                                    //    CipherSuite cipher_suites<2..2^16-2>;
                                    //    CompressionMethod compression_methods<1..2^8-1>;
                                    //    select (extensions_present) {
                                    //        case false:
                                    //            struct {};
                                    //        case true:
                                    //            Extension extensions<0..2^16-1>;
                                    //    };
                                    // } ClientHello;
                                    //

                                    int endOffset = readerIndex + packetLength;
                                    int offset = readerIndex + 43;

                                    if (endOffset - offset < 6)
                                    {
                                        goto LOOP_BREAK;
                                    }

                                    int sessionIdLength = input.GetByte(offset);
                                    offset += sessionIdLength + 1;

                                    int cipherSuitesLength = input.GetUnsignedShort(offset);
                                    offset += cipherSuitesLength + 2;

                                    int compressionMethodLength = input.GetByte(offset);
                                    offset += compressionMethodLength + 1;

                                    int extensionsLength = input.GetUnsignedShort(offset);
                                    offset += 2;
                                     int extensionsLimit = offset + extensionsLength;

                                    if (extensionsLimit > endOffset)
                                    {
                                        // Extensions should never exceed the record boundary.
                                        goto LOOP_BREAK; 
                                    }

                                    for (;;)
                                    {
                                        if (extensionsLimit - offset < 4)
                                        {
                                            goto LOOP_BREAK;
                                        }

                                        int extensionType = input.GetUnsignedShort(offset);
                                        offset += 2;

                                        int extensionLength = input.GetUnsignedShort(offset);
                                        offset += 2;

                                        if (extensionsLimit - offset < extensionLength)
                                        {
                                            goto LOOP_BREAK;
                                        }

                                        // SNI
                                        // See https://tools.ietf.org/html/rfc6066#page-6
                                        if (extensionType == 0)
                                        {
                                            offset += 2;
                                            if (extensionsLimit - offset < 3)
                                            {
                                                goto LOOP_BREAK;
                                            }

                                            int serverNameType = input.GetByte(offset);
                                            offset++;

                                            if (serverNameType == 0)
                                            {
                                                int serverNameLength = input.GetUnsignedShort(offset);
                                                offset += 2;

                                                if (extensionsLimit - offset < serverNameLength)
                                                {
                                                    goto LOOP_BREAK;
                                                }

                                                string hostname = input.ToString(offset, serverNameLength, Encoding.ASCII);
                                                //try
                                                //{
                                                //    select(ctx, IDN.toASCII(hostname,
                                                //                            IDN.ALLOW_UNASSIGNED).toLowerCase(Locale.US));
                                                //}
                                                //catch (Throwable t)
                                                //{
                                                //    PlatformDependent.throwException(t);
                                                //}
                                                this.Select(context, hostname); // TODO: verify hostname encoding and case
                                                return;
                                            }
                                            else
                                            {
                                                // invalid enum value
                                                goto LOOP_BREAK;
                                            }
                                        }

                                        offset += extensionLength;
                                    }
                                }

                                break;
                            // Fall-through
                            default:
                                //not tls, ssl or application data, do not try sni
                                break;
                        }
                    }
                    LOOP_BREAK:
                    ;
                }
                catch (Exception e)
                {
                    // unexpected encoding, ignore sni and use default
                    if (Logger.DebugEnabled)
                    {
                        Logger.Debug($"Unexpected client hello packet: {ByteBufferUtil.HexDump(input)}", e);
                    }
                }
                // Just select the default certifcate
                this.Select(context, null);
            }
        }

        void Select(IChannelHandlerContext context, string hostName) => this.ReplaceHandler(context, hostName);

        void ReplaceHandler(IChannelHandlerContext context, string hostName)
        {
            var serverTlsSetting = new ServerTlsSettings(this.serverTlsSniSettings.CertificateSelector(hostName), this.serverTlsSniSettings.NegotiateClientCertificate, this.serverTlsSniSettings.CheckCertificateRevocation, this.serverTlsSniSettings.EnabledProtocols);
            var tlsHandler = new TlsHandler(this.sslStreamFactory, serverTlsSetting);
            context.Channel.Pipeline.Replace(this, nameof(TlsHandler), tlsHandler);
            tlsHandler = null;
            this.handlerReplaced = true;
        }

        public override void Read(IChannelHandlerContext context)
        {
            if (!this.handlerReplaced)
            {
                base.Read(context);
            }

            // Either handler is replaced or failed, so suppress read
        }
    }
}